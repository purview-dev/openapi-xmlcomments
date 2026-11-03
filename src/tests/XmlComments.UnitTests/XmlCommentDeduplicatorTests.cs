namespace Purview.OpenAPI.XmlComments;

public sealed class XmlCommentDeduplicatorTests
{
	[Test]
	public async Task Deduplicate_GivenIdInTwoFiles_RemovesEveryOccurrence()
	{
		// Arrange
		var shared = TestBuilder.TypeId();
		var first = TestBuilder.Documentation([shared, TestBuilder.TypeId()]);
		var second = TestBuilder.Documentation([shared, TestBuilder.TypeId()]);

		// Act
		XmlCommentDeduplicator.Deduplicate([first, second], []);

		// Assert
		await Assert.That(Ids(first)).DoesNotContain(shared);
		await Assert.That(Ids(second)).DoesNotContain(shared);
	}

	[Test]
	public async Task Deduplicate_GivenIdInTwoFiles_KeepsEveryOtherMember()
	{
		// Arrange
		var shared = TestBuilder.TypeId();
		var firstOnly = TestBuilder.TypeId();
		var secondOnly = TestBuilder.TypeId();
		var first = TestBuilder.Documentation([shared, firstOnly]);
		var second = TestBuilder.Documentation([secondOnly, shared]);

		// Act
		XmlCommentDeduplicator.Deduplicate([first, second], []);

		// Assert
		await Assert.That(Ids(first)).IsEquivalentTo([firstOnly]);
		await Assert.That(Ids(second)).IsEquivalentTo([secondOnly]);
	}

	[Test]
	public async Task Deduplicate_GivenUniqueIds_RemovesNothing()
	{
		// Arrange
		var first = TestBuilder.Documentation([TestBuilder.TypeId(), TestBuilder.TypeId()]);
		var second = TestBuilder.Documentation([TestBuilder.TypeId()]);

		// Act
		var removed = XmlCommentDeduplicator.Deduplicate([first, second], []);

		// Assert
		await Assert.That(removed).IsEmpty();
		await Assert.That(Ids(first)).Count().IsEqualTo(2);
		await Assert.That(Ids(second)).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Deduplicate_GivenExcludedIdInOneFile_RemovesItAsExcluded()
	{
		// Arrange
		var excluded = TestBuilder.TypeId();
		var file = TestBuilder.Documentation([excluded, TestBuilder.TypeId()]);

		// Act
		var removed = XmlCommentDeduplicator.Deduplicate([file], [excluded]);

		// Assert
		await Assert.That(Ids(file)).DoesNotContain(excluded);
		await Assert.That(removed).Count().IsEqualTo(1);
		await Assert.That(removed[0].Reason).IsEqualTo(RemovalReason.Excluded);
	}

	[Test]
	public async Task Deduplicate_GivenIdsThatDifferOnlyInReturnType_TreatsThemAsDuplicates()
	{
		// Arrange
		var first = TestBuilder.Documentation(["M:Ns.Type.Method~System.String"]);
		var second = TestBuilder.Documentation(["M:Ns.Type.Method~System.Int32"]);

		// Act
		var removed = XmlCommentDeduplicator.Deduplicate([first, second], []);

		// Assert
		await Assert.That(removed).Count().IsEqualTo(1);
		await Assert.That(removed[0].Id).IsEqualTo("M:Ns.Type.Method");
		await Assert.That(Ids(first)).IsEmpty();
		await Assert.That(Ids(second)).IsEmpty();
	}

	[Test]
	public async Task Deduplicate_GivenSeveralDuplicates_ReportsThemOrdinallyWithEverySource()
	{
		// Arrange
		const string later = "T:Zeta.Type";
		const string earlier = "T:Alpha.Type";
		var first = TestBuilder.Documentation([later, earlier], name: "First");
		var second = TestBuilder.Documentation([earlier, later], name: "Second");

		// Act
		var removed = XmlCommentDeduplicator.Deduplicate([first, second], []);

		// Assert
		await Assert.That(removed.Select(member => member.Id)).IsEquivalentTo([earlier, later]);
		await Assert.That(removed[0].Id).IsEqualTo(earlier);
		await Assert.That(removed[0].Sources).IsEquivalentTo(["First", "Second"]);
		await Assert.That(removed[0].Reason).IsEqualTo(RemovalReason.Duplicate);
	}

	[Test]
	public async Task FindProjectOverlap_GivenIdInProjectAndReference_ReturnsItWithTheReference()
	{
		// Arrange
		var shared = TestBuilder.TypeId();
		var project = TestBuilder.Documentation([shared, TestBuilder.TypeId()]);
		var reference = TestBuilder.Documentation([TestBuilder.TypeId(), shared], name: "Reference");

		// Act
		var overlap = XmlCommentDeduplicator.FindProjectOverlap(project, [reference]);

		// Assert
		await Assert.That(overlap).Count().IsEqualTo(1);
		await Assert.That(overlap[0].Key).IsEqualTo(shared);
		await Assert.That(overlap[0].Value).IsEqualTo("Reference");
	}

	[Test]
	public async Task FindProjectOverlap_GivenNoSharedId_ReturnsEmpty()
	{
		// Arrange
		var project = TestBuilder.Documentation([TestBuilder.TypeId()]);
		var reference = TestBuilder.Documentation([TestBuilder.TypeId()]);

		// Act
		var overlap = XmlCommentDeduplicator.FindProjectOverlap(project, [reference]);

		// Assert
		await Assert.That(overlap).IsEmpty();
	}

	static List<string> Ids(XmlDocumentation documentation) =>
		[.. documentation.Members.Select(member => (string)member.Attribute("name")!)];
}
