namespace Purview.OpenAPI.XmlComments;

/// <summary>
/// Scratch files for the tests that read and write real documentation files.
/// </summary>
static class TestFiles
{
	/// <summary>
	/// A new, empty directory under the system temporary folder.
	/// </summary>
	public static string CreateDirectory() => Directory.CreateTempSubdirectory("purview-openapi-xmlcomments-").FullName;

	/// <summary>
	/// Writes <paramref name="content"/> to <paramref name="fileName"/> in a new directory, or in
	/// <paramref name="directory"/> when given, and returns the file's full path.
	/// </summary>
	public static async Task<string> WriteAsync(
		string fileName,
		string content,
		CancellationToken cancellationToken,
		string? directory = null
	)
	{
		var path = Path.Combine(directory ?? CreateDirectory(), fileName);
		await File.WriteAllTextAsync(path, content, cancellationToken);

		return path;
	}
}
