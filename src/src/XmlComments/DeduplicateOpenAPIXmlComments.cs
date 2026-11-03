using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Purview.OpenAPI.XmlComments;

/// <summary>
/// Writes copies of the documentation files the OpenAPI XML-comment generator reads with every colliding
/// documentation ID removed, so its generated cache never adds the same key twice.
/// </summary>
/// <remarks>
/// Run by the <c>PurviewDeduplicateOpenAPIXmlComments</c> target after the OpenAPI package's
/// <c>GenerateAdditionalXmlFilesForOpenApi</c> target, which swaps <see cref="Replaced"/> out of
/// <c>AdditionalFiles</c> and <see cref="Replacements"/> in. A file with nothing to remove is not copied.
/// </remarks>
[SuppressMessage(
	"Performance",
	"CA1819:Properties should not return arrays",
	Justification = "MSBuild binds item parameters to ITaskItem[] only."
)]
public sealed class DeduplicateOpenAPIXmlComments : Microsoft.Build.Utilities.Task
{
	/// <summary>
	/// Gets or sets the candidate files: the <c>.xml</c> items in <c>AdditionalFiles</c>. Anything that is not an XML
	/// documentation file is ignored.
	/// </summary>
	[Required]
	public ITaskItem[] Files { get; set; } = [];

	/// <summary>
	/// Gets or sets documentation IDs to remove even when they occur once (<c>@(PurviewOpenAPIXmlCommentExclude)</c>).
	/// </summary>
	public ITaskItem[] Exclude { get; set; } = [];

	/// <summary>
	/// Gets or sets the absolute directory the filtered copies are written to.
	/// </summary>
	[Required]
	public string OutputDirectory { get; set; } = string.Empty;

	/// <summary>
	/// Gets the items from <see cref="Files"/> that were copied, so they can be removed from <c>AdditionalFiles</c>.
	/// </summary>
	[Output]
	public ITaskItem[] Replaced { get; private set; } = [];

	/// <summary>
	/// Gets the filtered copies, in the same order as <see cref="Replaced"/>.
	/// </summary>
	[Output]
	public ITaskItem[] Replacements { get; private set; } = [];

	/// <inheritdoc />
	public override bool Execute()
	{
		List<ITaskItem> items = [];
		List<XmlDocumentation> files = [];
		foreach (var item in Files)
		{
			var documentation = XmlDocumentation.Load(item.GetMetadata("FullPath"));
			if (documentation is not null)
			{
				items.Add(item);
				files.Add(documentation);
			}
		}

		// Counted per file rather than matched by name, because two references can share a file name.
		var memberCounts = files.Select(file => file.Members.Count()).ToList();
		var removed = XmlCommentDeduplicator.Deduplicate(files, Exclude.Select(item => item.ItemSpec));
		foreach (var member in removed)
		{
			Log.LogMessage(
				MessageImportance.Normal,
				"OpenAPI XML comments: removed '{0}' from {1} ({2}).",
				member.Id,
				string.Join(", ", member.Sources),
				member.Reason == RemovalReason.Excluded ? "excluded" : "duplicate"
			);
		}

		List<ITaskItem> replaced = [];
		List<ITaskItem> replacements = [];
		HashSet<string> fileNames = new(StringComparer.OrdinalIgnoreCase);

		for (var i = 0; i < files.Count; i++)
		{
			if (files[i].Members.Count() == memberCounts[i])
			{
				continue;
			}

			var path = Path.Combine(OutputDirectory, UniqueFileName(files[i].Name, fileNames));
			WriteIfChanged(path, files[i].ToFileText());

			replaced.Add(items[i]);
			replacements.Add(new TaskItem(path));
		}

		Replaced = [.. replaced];
		Replacements = [.. replacements];

		return !Log.HasLoggedErrors;
	}

	/// <summary>
	/// Two references can share a file name (the same assembly name from different folders), so a later one gets a
	/// numeric suffix rather than overwriting the first.
	/// </summary>
	static string UniqueFileName(string name, HashSet<string> used)
	{
		var fileName = name + ".xml";
		for (var i = 1; !used.Add(fileName); i++)
		{
			fileName = $"{name}.{i}.xml";
		}

		return fileName;
	}

	/// <summary>
	/// The copies are compiler inputs, so a fresh timestamp on every build would defeat incremental compilation. A copy
	/// is only written when its content changes.
	/// </summary>
	static void WriteIfChanged(string path, string text)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		if (!File.Exists(path) || File.ReadAllText(path) != text)
		{
			File.WriteAllText(path, text);
		}
	}
}
