namespace Purview.OpenAPI.XmlComments;

public sealed class XmlDocumentationTests
{
	[Test]
	public async Task Load_GivenDocumentationFile_ReturnsItNamedAfterTheFile(CancellationToken cancellationToken)
	{
		// Arrange
		var path = await TestFiles.WriteAsync(
			"Lib.A.xml",
			TestBuilder.DocumentationText([TestBuilder.TypeId()]),
			cancellationToken
		);

		// Act
		var documentation = XmlDocumentation.Load(path);

		// Assert
		await Assert.That(documentation).IsNotNull();
		await Assert.That(documentation!.Name).IsEqualTo("Lib.A");
	}

	[Test]
	[Arguments("<configuration><appSettings /></configuration>")]
	[Arguments("<doc><assembly /></doc>")]
	[Arguments("<doc><members>")]
	public async Task Load_GivenXmlThatIsNotADocumentationFile_ReturnsNull(
		string content,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var path = await TestFiles.WriteAsync("Other.xml", content, cancellationToken);

		// Act
		var documentation = XmlDocumentation.Load(path);

		// Assert
		await Assert.That(documentation).IsNull();
	}

	[Test]
	public async Task Load_GivenMissingFile_ReturnsNull()
	{
		// Arrange
		var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Missing.xml");

		// Act
		var documentation = XmlDocumentation.Load(path);

		// Assert
		await Assert.That(documentation).IsNull();
	}

	[Test]
	public async Task ToFileText_GivenRemovedMember_OmitsItAndKeepsTheRest()
	{
		// Arrange
		var removed = TestBuilder.TypeId();
		var kept = TestBuilder.TypeId();
		var documentation = TestBuilder.Documentation([removed, kept]);
		documentation.Members.First().Remove();

		// Act
		var text = documentation.ToFileText();

		// Assert
		await Assert.That(text).DoesNotContain(removed);
		await Assert.That(text).Contains(kept);
		await Assert.That(text).StartsWith("<?xml version=\"1.0\"?>");
	}
}
