using Microsoft.Build.Framework;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Purview.OpenAPI.XmlComments;

public sealed class DeduplicateOpenAPIXmlCommentsTests
{
	[Test]
	public async Task Execute_GivenIdInTwoFiles_WritesFilteredCopiesOfBoth(CancellationToken cancellationToken)
	{
		// Arrange
		var shared = TestBuilder.TypeId();
		var first = await TestFiles.WriteAsync(
			"Lib.A.xml",
			TestBuilder.DocumentationText([shared, TestBuilder.TypeId()]),
			cancellationToken
		);
		var second = await TestFiles.WriteAsync(
			"Lib.B.xml",
			TestBuilder.DocumentationText([shared]),
			cancellationToken
		);
		var task = CreateTask([first, second]);

		// Act
		var succeeded = task.Execute();

		// Assert
		await Assert.That(succeeded).IsTrue();
		await Assert.That(task.Replaced.Select(item => item.ItemSpec)).IsEquivalentTo([first, second]);
		await Assert
			.That(task.Replacements.Select(item => Path.GetFileName(item.ItemSpec)))
			.IsEquivalentTo(["Lib.A.xml", "Lib.B.xml"]);
		foreach (var replacement in task.Replacements)
		{
			await Assert.That(Path.GetDirectoryName(replacement.ItemSpec)).IsEqualTo(task.OutputDirectory);
			await Assert
				.That(await File.ReadAllTextAsync(replacement.ItemSpec, cancellationToken))
				.DoesNotContain(shared);
		}
	}

	[Test]
	public async Task Execute_GivenNoCollidingIds_ReplacesNothing(CancellationToken cancellationToken)
	{
		// Arrange
		var first = await TestFiles.WriteAsync(
			"Lib.A.xml",
			TestBuilder.DocumentationText([TestBuilder.TypeId()]),
			cancellationToken
		);
		var second = await TestFiles.WriteAsync(
			"Lib.B.xml",
			TestBuilder.DocumentationText([TestBuilder.TypeId()]),
			cancellationToken
		);
		var task = CreateTask([first, second]);

		// Act
		var succeeded = task.Execute();

		// Assert
		await Assert.That(succeeded).IsTrue();
		await Assert.That(task.Replaced).IsEmpty();
		await Assert.That(task.Replacements).IsEmpty();
	}

	[Test]
	public async Task Execute_GivenExcludedId_ReplacesTheOneFileThatHasIt(CancellationToken cancellationToken)
	{
		// Arrange
		var excluded = TestBuilder.TypeId();
		var first = await TestFiles.WriteAsync(
			"Lib.A.xml",
			TestBuilder.DocumentationText([excluded]),
			cancellationToken
		);
		var second = await TestFiles.WriteAsync(
			"Lib.B.xml",
			TestBuilder.DocumentationText([TestBuilder.TypeId()]),
			cancellationToken
		);
		var task = CreateTask([first, second], exclude: [excluded]);

		// Act
		task.Execute();

		// Assert
		await Assert.That(task.Replaced.Select(item => item.ItemSpec)).IsEquivalentTo([first]);
	}

	[Test]
	public async Task Execute_GivenXmlThatIsNotDocumentation_LeavesItInPlace(CancellationToken cancellationToken)
	{
		// Arrange
		var shared = TestBuilder.TypeId();
		var other = await TestFiles.WriteAsync("settings.xml", "<configuration />", cancellationToken);
		var first = await TestFiles.WriteAsync("Lib.A.xml", TestBuilder.DocumentationText([shared]), cancellationToken);
		var second = await TestFiles.WriteAsync(
			"Lib.B.xml",
			TestBuilder.DocumentationText([shared]),
			cancellationToken
		);
		var task = CreateTask([other, first, second]);

		// Act
		task.Execute();

		// Assert
		await Assert.That(task.Replaced.Select(item => item.ItemSpec)).DoesNotContain(other);
	}

	[Test]
	public async Task Execute_GivenTwoFilesWithTheSameName_WritesDistinctCopies(CancellationToken cancellationToken)
	{
		// Arrange
		var shared = TestBuilder.TypeId();
		var first = await TestFiles.WriteAsync("Lib.xml", TestBuilder.DocumentationText([shared]), cancellationToken);
		var second = await TestFiles.WriteAsync("Lib.xml", TestBuilder.DocumentationText([shared]), cancellationToken);
		var task = CreateTask([first, second]);

		// Act
		task.Execute();

		// Assert
		await Assert
			.That(task.Replacements.Select(item => Path.GetFileName(item.ItemSpec)))
			.IsEquivalentTo(["Lib.xml", "Lib.1.xml"]);
	}

	[Test]
	public async Task Execute_GivenUnchangedInputs_DoesNotRewriteTheCopy(CancellationToken cancellationToken)
	{
		// Arrange
		var shared = TestBuilder.TypeId();
		var first = await TestFiles.WriteAsync("Lib.A.xml", TestBuilder.DocumentationText([shared]), cancellationToken);
		var second = await TestFiles.WriteAsync(
			"Lib.B.xml",
			TestBuilder.DocumentationText([shared]),
			cancellationToken
		);
		var outputDirectory = TestFiles.CreateDirectory();
		CreateTask([first, second], outputDirectory: outputDirectory).Execute();

		var copy = Path.Combine(outputDirectory, "Lib.A.xml");
		DateTime stamped = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		File.SetLastWriteTimeUtc(copy, stamped);

		// Act
		CreateTask([first, second], outputDirectory: outputDirectory).Execute();

		// Assert
		await Assert.That(File.GetLastWriteTimeUtc(copy)).IsEqualTo(stamped);
	}

	static DeduplicateOpenAPIXmlComments CreateTask(
		IEnumerable<string> files,
		IEnumerable<string>? exclude = null,
		string? outputDirectory = null
	) =>
		new()
		{
			BuildEngine = Mock.Of<IBuildEngine>().Object,
			Files = [.. files.Select(file => (ITaskItem)new TaskItem(file))],
			Exclude = [.. (exclude ?? []).Select(id => (ITaskItem)new TaskItem(id))],
			OutputDirectory = outputDirectory ?? TestFiles.CreateDirectory(),
		};
}
