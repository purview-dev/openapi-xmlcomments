using Microsoft.Build.Framework;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Purview.OpenAPI.XmlComments;

public sealed class CheckOpenAPIXmlCommentOverlapTests
{
	[Test]
	public async Task Execute_GivenIdInProjectAndReference_LogsOverlapWarning(CancellationToken cancellationToken)
	{
		// Arrange
		var shared = TestBuilder.TypeId();
		var project = await TestFiles.WriteAsync("Api.xml", TestBuilder.DocumentationText([shared]), cancellationToken);
		var reference = await TestFiles.WriteAsync(
			"Lib.A.xml",
			TestBuilder.DocumentationText([shared]),
			cancellationToken
		);
		var engine = Mock.Of<IBuildEngine>();
		var task = CreateTask(engine.Object, project, [reference]);

		// Act
		var succeeded = task.Execute();

		// Assert
		await Assert.That(succeeded).IsTrue();
		engine
			.LogWarningEvent(
				Is<BuildWarningEventArgs>(warning =>
					warning is { Code: CheckOpenAPIXmlCommentOverlap.OverlapWarningCode, Message: { } message }
					&& message.Contains(shared, StringComparison.Ordinal)
					&& message.Contains("Lib.A.xml", StringComparison.Ordinal)
				)
			)
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task Execute_GivenNoSharedId_LogsNoWarning(CancellationToken cancellationToken)
	{
		// Arrange
		var project = await TestFiles.WriteAsync(
			"Api.xml",
			TestBuilder.DocumentationText([TestBuilder.TypeId()]),
			cancellationToken
		);
		var reference = await TestFiles.WriteAsync(
			"Lib.A.xml",
			TestBuilder.DocumentationText([TestBuilder.TypeId()]),
			cancellationToken
		);
		var engine = Mock.Of<IBuildEngine>();
		var task = CreateTask(engine.Object, project, [reference]);

		// Act
		var succeeded = task.Execute();

		// Assert
		await Assert.That(succeeded).IsTrue();
		engine.LogWarningEvent(Any()).WasNeverCalled();
	}

	[Test]
	public async Task Execute_GivenMissingProjectDocumentation_SucceedsWithoutWarning(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var reference = await TestFiles.WriteAsync(
			"Lib.A.xml",
			TestBuilder.DocumentationText([TestBuilder.TypeId()]),
			cancellationToken
		);
		var engine = Mock.Of<IBuildEngine>();
		var task = CreateTask(engine.Object, Path.Combine(TestFiles.CreateDirectory(), "Api.xml"), [reference]);

		// Act
		var succeeded = task.Execute();

		// Assert
		await Assert.That(succeeded).IsTrue();
		engine.LogWarningEvent(Any()).WasNeverCalled();
	}

	static CheckOpenAPIXmlCommentOverlap CreateTask(
		IBuildEngine engine,
		string project,
		IEnumerable<string> references
	) =>
		new()
		{
			BuildEngine = engine,
			ProjectDocumentation = project,
			ReferenceDocumentation = [.. references.Select(reference => (ITaskItem)new TaskItem(reference))],
		};
}
