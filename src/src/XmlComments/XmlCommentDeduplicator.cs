namespace Purview.OpenAPI.XmlComments;

/// <summary>
/// Why a documentation ID was removed.
/// </summary>
enum RemovalReason
{
	/// <summary>The ID occurs more than once across the documentation files.</summary>
	Duplicate,

	/// <summary>The ID was listed in <c>PurviewOpenAPIXmlCommentExclude</c>.</summary>
	Excluded,
}

/// <summary>
/// A documentation ID that was removed, and the files it was removed from.
/// </summary>
/// <param name="id">The normalised documentation ID.</param>
/// <param name="sources">The names of the files it was removed from, in input order, once per occurrence.</param>
/// <param name="reason">Why it was removed.</param>
sealed class RemovedMember(string id, IReadOnlyList<string> sources, RemovalReason reason)
{
	/// <summary>
	/// Gets the normalised documentation ID.
	/// </summary>
	public string Id { get; } = id;

	/// <summary>
	/// Gets the names of the files the ID was removed from, in input order, once per occurrence.
	/// </summary>
	public IReadOnlyList<string> Sources { get; } = sources;

	/// <summary>
	/// Gets why the ID was removed.
	/// </summary>
	public RemovalReason Reason { get; } = reason;
}

/// <summary>
/// Removes the documentation IDs that would collide in the OpenAPI XML-comment generator's cache.
/// </summary>
/// <remarks>
/// The generator adds every <c>&lt;member&gt;</c> of every documentation file in <c>AdditionalFiles</c>, and every
/// public documented symbol of the project's own compilation, to one dictionary with <c>Add</c>, so any ID that
/// arrives twice throws when the cache is first used. A type that a source generator or polyfill injects into every
/// assembly arrives once per referenced assembly, and once more from the project itself.
/// </remarks>
static class XmlCommentDeduplicator
{
	/// <summary>
	/// Removes, in place, every occurrence of each ID that occurs more than once across <paramref name="files"/>, and
	/// every occurrence of each ID in <paramref name="excludedIds"/>.
	/// </summary>
	/// <param name="files">The documentation files, in the order the generator receives them.</param>
	/// <param name="excludedIds">IDs to remove even when they occur once. They are normalised before matching.</param>
	/// <returns>The removed IDs, ordered ordinally.</returns>
	/// <remarks>
	/// <para>
	/// Every occurrence is removed, not all but the first. When the project's own compilation also declares the type
	/// (the usual case for generator-injected types) it contributes the only remaining copy; keeping a reference's copy
	/// as well would still collide with it.
	/// </para>
	/// <para>
	/// A duplicated ID is ambient code (an injected attribute, a polyfill), not API surface, so removing it loses no
	/// endpoint or schema description.
	/// </para>
	/// </remarks>
	public static IReadOnlyList<RemovedMember> Deduplicate(
		IReadOnlyList<XmlDocumentation> files,
		IEnumerable<string> excludedIds
	)
	{
		HashSet<string> excluded = new(excludedIds.Select(DocumentationId.Normalize), StringComparer.Ordinal);
		Dictionary<string, List<string>> sources = new(StringComparer.Ordinal);

		foreach (var file in files)
		{
			foreach (var member in file.Members)
			{
				var id = XmlDocumentation.IdOf(member);
				if (!sources.TryGetValue(id, out var list))
				{
					sources[id] = list = [];
				}

				list.Add(file.Name);
			}
		}

		var removed = sources
			.Where(source => source.Value.Count > 1 || excluded.Contains(source.Key))
			.OrderBy(source => source.Key, StringComparer.Ordinal)
			.Select(source => new RemovedMember(
				source.Key,
				source.Value,
				excluded.Contains(source.Key) ? RemovalReason.Excluded : RemovalReason.Duplicate
			))
			.ToList();

		if (removed.Count == 0)
		{
			return removed;
		}

		HashSet<string> removedIds = new(removed.Select(member => member.Id), StringComparer.Ordinal);
		foreach (var file in files)
		{
			// Materialised first: removing an element while enumerating its siblings ends the enumeration early.
			foreach (
				var member in file.Members.Where(member => removedIds.Contains(XmlDocumentation.IdOf(member))).ToList()
			)
			{
				member.Remove();
			}
		}

		return removed;
	}

	/// <summary>
	/// Finds the IDs documented both in <paramref name="project"/> and in <paramref name="references"/>.
	/// </summary>
	/// <param name="project">The project's own documentation file.</param>
	/// <param name="references">The documentation files the generator reads, after deduplication.</param>
	/// <returns>Each overlapping ID with the reference file it also appears in, ordered by file then ID.</returns>
	/// <remarks>
	/// The project's own compilation is only known once it compiles, so <see cref="Deduplicate"/> cannot see it. An
	/// overlap only collides when the project's copy is public, because the generator reads public symbols from its
	/// own compilation, so this can over-report.
	/// </remarks>
	public static IReadOnlyList<KeyValuePair<string, string>> FindProjectOverlap(
		XmlDocumentation project,
		IReadOnlyList<XmlDocumentation> references
	)
	{
		HashSet<string> own = new(project.Members.Select(XmlDocumentation.IdOf), StringComparer.Ordinal);

		return
		[
			.. references.SelectMany(reference =>
				reference
					.Members.Select(XmlDocumentation.IdOf)
					.Where(own.Contains)
					.Distinct(StringComparer.Ordinal)
					.OrderBy(id => id, StringComparer.Ordinal)
					.Select(id => new KeyValuePair<string, string>(id, reference.Name))
			),
		];
	}
}
