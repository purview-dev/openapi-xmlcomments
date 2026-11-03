using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Purview.OpenAPI.XmlComments;

/// <summary>
/// A compiler-produced XML documentation file: a <c>&lt;doc&gt;</c> root holding <c>&lt;members&gt;</c>.
/// </summary>
/// <param name="name">A display name for the file, used in log messages.</param>
/// <param name="document">The loaded document. It must be a documentation file (see <see cref="IsDocumentation"/>).</param>
sealed class XmlDocumentation(string name, XDocument document)
{
	/// <summary>
	/// Gets the display name for the file, used in log messages.
	/// </summary>
	public string Name { get; } = name;

	/// <summary>
	/// Gets the loaded document.
	/// </summary>
	public XDocument Document { get; } = document;

	/// <summary>
	/// Gets the file's <c>&lt;member&gt;</c> elements that carry a <c>name</c>.
	/// </summary>
	public IEnumerable<XElement> Members =>
		Document
			.Root.Element("members")
			.Elements("member")
			.Where(member => !string.IsNullOrEmpty((string?)member.Attribute("name")));

	/// <summary>
	/// Gets the documentation ID of <paramref name="member"/>, normalised as the OpenAPI generator keys it.
	/// </summary>
	/// <param name="member">One of <see cref="Members"/>.</param>
	/// <returns>The normalised documentation ID.</returns>
	public static string IdOf(XElement member) => DocumentationId.Normalize((string)member.Attribute("name"));

	/// <summary>
	/// Determines whether <paramref name="document"/> is an XML documentation file.
	/// </summary>
	/// <param name="document">The document to test.</param>
	/// <returns><see langword="true"/> for a <c>&lt;doc&gt;</c> root holding <c>&lt;members&gt;</c>.</returns>
	/// <remarks>
	/// <c>AdditionalFiles</c> can hold any XML, so everything else is left for the generator to read untouched.
	/// </remarks>
	public static bool IsDocumentation(XDocument document) =>
		document.Root is { Name.LocalName: "doc" } root && root.Element("members") is not null;

	/// <summary>
	/// Loads <paramref name="path"/> as an XML documentation file.
	/// </summary>
	/// <param name="path">The file to load.</param>
	/// <returns>
	/// The documentation file when <paramref name="path"/> exists, parses and is a documentation file; otherwise
	/// <see langword="null"/>, so an unreadable file is passed through rather than failing the build.
	/// </returns>
	public static XmlDocumentation? Load(string path)
	{
		try
		{
			var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);

			return IsDocumentation(document) ? new(Path.GetFileNameWithoutExtension(path), document) : null;
		}
		catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	/// <summary>
	/// Renders the document as the text to write to disk.
	/// </summary>
	/// <returns>The XML declaration (when present) and the root element, without reformatting.</returns>
	public string ToFileText()
	{
		StringBuilder text = new();
		if (Document.Declaration is not null)
		{
			text.Append(Document.Declaration).Append('\n');
		}

		return text.Append(Document.Root.ToString(SaveOptions.DisableFormatting)).Append('\n').ToString();
	}
}
