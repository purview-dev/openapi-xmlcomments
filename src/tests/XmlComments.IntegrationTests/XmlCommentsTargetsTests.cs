using Purview.OpenAPI.XmlComments.Harness;

namespace Purview.OpenAPI.XmlComments;

/// <summary>
/// Builds and runs real consumers of <c>Purview.OpenAPI.XmlComments.props</c>/<c>.targets</c> against the real OpenAPI
/// XML-comment generator.
/// </summary>
/// <remarks>
/// Every consumer compiles the same documented public attribute into the API and each library, which is what a source
/// generator's post-initialization output or a polyfill does, and what makes the generated cache throw.
/// </remarks>
public sealed class XmlCommentsTargetsTests
{
	const int BuildTimeout = 10 * 60 * 1000;

	[Test]
	[Timeout(BuildTimeout)]
	public async Task Build_GivenDeduplicationDisabled_ReproducesTheDuplicateKeyFailure(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var solution = await ConsumerSolution.CreateAsync(new(Deduplication: false), cancellationToken);

		// Act
		var build = await solution.BuildAsync(cancellationToken);
		var run = await solution.RunAPIAsync(cancellationToken);

		// Assert
		await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
		await Assert
			.That(await solution.CountCacheEntriesAsync(ConsumerSolution.InjectedTypeId, cancellationToken))
			.IsEqualTo(3);
		await Assert.That(run.ExitCode).IsNotEqualTo(0);
		await Assert.That(run.Output).Contains("An item with the same key has already been added");
	}

	[Test]
	[Timeout(BuildTimeout)]
	[Arguments("net10.0")]
	[Arguments("net11.0")]
	public async Task Build_GivenTypeInEveryAssembly_ServesTheDocumentWithEveryDescription(
		string targetFramework,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var solution = await ConsumerSolution.CreateAsync(new(TargetFramework: targetFramework), cancellationToken);

		// Act
		var build = await solution.BuildAsync(cancellationToken);
		var run = await solution.RunAPIAsync(cancellationToken);

		// Assert
		await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
		await Assert.That(build.Output).DoesNotContain(CheckOpenAPIXmlCommentOverlap.OverlapWarningCode);
		await Assert
			.That(await solution.CountCacheEntriesAsync(ConsumerSolution.InjectedTypeId, cancellationToken))
			.IsEqualTo(1);
		await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
		await Assert.That(run.Output).Contains("Widget from library A.");
		await Assert.That(run.Output).Contains("Widget from library B.");
	}

	[Test]
	[Timeout(BuildTimeout)]
	public async Task Build_GivenTypeInOneReferenceAndTheProject_WarnsWithTheExcludeToAdd(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var solution = await ConsumerSolution.CreateAsync(new(LibraryCount: 1), cancellationToken);

		// Act
		var build = await solution.BuildAsync(cancellationToken);

		// Assert
		await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
		await Assert.That(build.Output).Contains($"warning {CheckOpenAPIXmlCommentOverlap.OverlapWarningCode}");
		await Assert
			.That(build.Output)
			.Contains($"<PurviewOpenAPIXmlCommentExclude Include=\"{ConsumerSolution.InjectedTypeId}\" />");
	}

	[Test]
	[Timeout(BuildTimeout)]
	public async Task Build_GivenExcludedId_ServesTheDocumentWithoutWarning(CancellationToken cancellationToken)
	{
		// Arrange
		var solution = await ConsumerSolution.CreateAsync(
			new(LibraryCount: 1, ExcludedIds: [ConsumerSolution.InjectedTypeId]),
			cancellationToken
		);

		// Act
		var build = await solution.BuildAsync(cancellationToken);
		var run = await solution.RunAPIAsync(cancellationToken);

		// Assert
		await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
		await Assert.That(build.Output).DoesNotContain(CheckOpenAPIXmlCommentOverlap.OverlapWarningCode);
		await Assert
			.That(await solution.CountCacheEntriesAsync(ConsumerSolution.InjectedTypeId, cancellationToken))
			.IsEqualTo(1);
		await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
	}

	[Test]
	[Timeout(BuildTimeout)]
	public async Task Build_GivenUnchangedInputs_DoesNotRecompileTheAPI(CancellationToken cancellationToken)
	{
		// Arrange
		var solution = await ConsumerSolution.CreateAsync(new(), cancellationToken);
		var first = await solution.BuildAsync(cancellationToken);
		var assembly = Path.Combine(solution.APIIntermediateDirectory, "Api.dll");
		var compiled = File.GetLastWriteTimeUtc(assembly);

		// Act
		var second = await solution.BuildAsync(cancellationToken);

		// Assert
		await Assert.That(first.ExitCode).IsEqualTo(0).Because(first.Output);
		await Assert.That(second.ExitCode).IsEqualTo(0).Because(second.Output);
		await Assert.That(File.GetLastWriteTimeUtc(assembly)).IsEqualTo(compiled);
	}

	[Test]
	[Timeout(BuildTimeout)]
	public async Task Build_GivenProjectWithoutOpenAPI_LeavesItUntouched(CancellationToken cancellationToken)
	{
		// Arrange
		var solution = await ConsumerSolution.CreateAsync(new(ReferencesOpenAPI: false), cancellationToken);

		// Act
		var build = await solution.BuildAsync(cancellationToken);

		// Assert
		await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
		await Assert
			.That(Directory.Exists(Path.Combine(solution.APIIntermediateDirectory, "purview-openapi-xml")))
			.IsFalse();
		await Assert.That(build.Output).DoesNotContain(CheckOpenAPIXmlCommentOverlap.OverlapWarningCode);
	}
}
