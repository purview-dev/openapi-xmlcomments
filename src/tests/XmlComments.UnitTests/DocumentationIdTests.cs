namespace Purview.OpenAPI.XmlComments;

public sealed class DocumentationIdTests
{
	[Test]
	[Arguments("T:Ns.Type")]
	[Arguments("P:Ns.Type.Value")]
	[Arguments("M:Ns.Type.Method(System.Int32)")]
	public async Task Normalize_GivenIdWithoutReturnType_ReturnsItUnchanged(string id)
	{
		// Arrange
		// Act
		var normalized = DocumentationId.Normalize(id);

		// Assert
		await Assert.That(normalized).IsEqualTo(id);
	}

	[Test]
	public async Task Normalize_GivenMethodWithReturnType_RemovesTheReturnType()
	{
		// Arrange
		const string id = "M:Ns.Type.Method(System.Int32)~System.String";

		// Act
		var normalized = DocumentationId.Normalize(id);

		// Assert
		await Assert.That(normalized).IsEqualTo("M:Ns.Type.Method(System.Int32)");
	}

	[Test]
	[Arguments("M:Ns.Type.op_Implicit(System.Int32)~Ns.Type")]
	[Arguments("M:Ns.Type.op_Explicit(Ns.Type)~System.Int32")]
	public async Task Normalize_GivenConversionOperator_KeepsTheReturnType(string id)
	{
		// Arrange
		// Act
		var normalized = DocumentationId.Normalize(id);

		// Assert
		await Assert.That(normalized).IsEqualTo(id);
	}
}
