namespace Purview.OpenAPI.XmlComments;

/// <summary>
/// Documentation-comment IDs as the OpenAPI XML-comment generator keys them.
/// </summary>
static class DocumentationId
{
	/// <summary>
	/// Normalises <paramref name="id"/> the way <c>XmlCommentGenerator.NormalizeDocId</c> does before using it as a
	/// cache key: a method's <c>~ReturnType</c> suffix is removed, except on conversion operators, whose return type
	/// is what tells their overloads apart.
	/// </summary>
	/// <param name="id">The documentation-comment ID, for example <c>M:Ns.Type.Method(System.Int32)~System.String</c>.</param>
	/// <returns>The ID the generator would use as its cache key.</returns>
	/// <remarks>
	/// Duplicates are judged on this form, because two IDs that differ only in a stripped suffix still collide in the
	/// generator's dictionary.
	/// </remarks>
	public static string Normalize(string id)
	{
		var tilde = id.IndexOf('~');
		if (
			tilde < 0
			|| !id.StartsWith("M:", StringComparison.Ordinal)
			|| id.Contains(".op_Implicit(")
			|| id.Contains(".op_Explicit(")
		)
		{
			return id;
		}

		return id.Substring(0, tilde);
	}
}
