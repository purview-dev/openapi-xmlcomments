using System.Xml.Linq;

namespace Purview.OpenAPI.XmlComments;

/// <summary>
/// Builds the XML documentation content the tests work on.
/// </summary>
static class TestBuilder
{
	internal static Faker Faker { get; } = new();

	/// <summary>
	/// A type documentation ID, <c>T:Namespace.Type</c>.
	/// </summary>
	public static string TypeId(string? fullName = null) =>
		$"T:{fullName ?? $"{Faker.Lorem.Word()}.{Faker.Lorem.Word()}{Faker.Random.AlphaNumeric(8)}"}";

	/// <summary>
	/// A compiler-shaped XML documentation file holding one <c>&lt;member&gt;</c> per ID, each with a generated summary.
	/// </summary>
	public static XmlDocumentation Documentation(IEnumerable<string> memberIds, string? name = null) =>
		new(
			name ?? AssemblyName(),
			XDocument.Parse(DocumentationText(memberIds, name), LoadOptions.PreserveWhitespace)
		);

	/// <summary>
	/// The text of a compiler-shaped XML documentation file holding one <c>&lt;member&gt;</c> per ID.
	/// </summary>
	public static string DocumentationText(IEnumerable<string> memberIds, string? assemblyName = null)
	{
		var members = string.Concat(
			memberIds.Select(id =>
				$"""

							<member name="{id}">
								<summary>{Faker.Lorem.Sentence()}</summary>
							</member>
					"""
			)
		);

		return $"""
			<?xml version="1.0"?>
			<doc>
				<assembly>
					<name>{assemblyName ?? AssemblyName()}</name>
				</assembly>
				<members>{members}
				</members>
			</doc>
			""";
	}

	/// <summary>
	/// An assembly name.
	/// </summary>
	public static string AssemblyName() => $"{Faker.Lorem.Word()}.{Faker.Lorem.Word()}";
}
