using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;

namespace Purview.OpenAPI.XmlComments;

/// <summary>
/// Warns when a documentation ID is documented both in the project and in a documentation file the OpenAPI
/// XML-comment generator reads, which the deduplication run before compile cannot see.
/// </summary>
/// <remarks>
/// Run by the <c>PurviewCheckOpenAPIXmlCommentOverlap</c> target after <c>CoreCompile</c>, against the project's
/// freshly written documentation file. The fix it suggests, <c>PurviewOpenAPIXmlCommentExclude</c>, removes the
/// reference's copy on the next build.
/// </remarks>
[SuppressMessage(
	"Performance",
	"CA1819:Properties should not return arrays",
	Justification = "MSBuild binds item parameters to ITaskItem[] only."
)]
public sealed class CheckOpenAPIXmlCommentOverlap : Microsoft.Build.Utilities.Task
{
	/// <summary>
	/// The warning code for an ID documented in both the project and a reference.
	/// </summary>
	public const string OverlapWarningCode = "POAX001";

	/// <summary>
	/// Gets or sets the path of the project's own XML documentation file (<c>@(DocFileItem)</c>).
	/// </summary>
	[Required]
	public string ProjectDocumentation { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the documentation files the generator reads, after deduplication.
	/// </summary>
	[Required]
	public ITaskItem[] ReferenceDocumentation { get; set; } = [];

	/// <inheritdoc />
	public override bool Execute()
	{
		var project = XmlDocumentation.Load(ProjectDocumentation);
		if (project is null)
		{
			return true;
		}

		List<XmlDocumentation> references = [];
		foreach (var item in ReferenceDocumentation)
		{
			var documentation = XmlDocumentation.Load(item.GetMetadata("FullPath"));
			if (documentation is not null)
			{
				references.Add(documentation);
			}
		}

		foreach (var overlap in XmlCommentDeduplicator.FindProjectOverlap(project, references))
		{
			Log.LogWarning(
				subcategory: null,
				warningCode: OverlapWarningCode,
				helpKeyword: null,
				file: null,
				lineNumber: 0,
				columnNumber: 0,
				endLineNumber: 0,
				endColumnNumber: 0,
				message: "'{0}' is documented both in this project and in {1}.xml. If this project's copy is public, the OpenAPI XML-comment cache throws \"An item with the same key has already been added\" at run time; add <PurviewOpenAPIXmlCommentExclude Include=\"{0}\" /> to drop the reference's copy.",
				overlap.Key,
				overlap.Value
			);
		}

		return !Log.HasLoggedErrors;
	}
}
