using System.Text.Json;
using Purview.OpenAPI.XmlComments.Harness;
using Purview.OpenAPI.XmlComments.Infra;

namespace Purview.OpenAPI.XmlComments;

/// <summary>
/// Runs the <c>purview-openapi-xmlcomments-duplicates</c> skill's diagnostic script against real consumers, so the
/// skill's instructions stay true to what the script reports.
/// </summary>
public sealed class FindDuplicateDocumentationIdsScriptTests
{
	const int Timeout = 10 * 60 * 1000;

	[Test]
	[Timeout(Timeout)]
	public async Task Script_GivenDuplicateBetweenReferencesWithDeduplicationOff_ReportsItUnhandled(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var solution = await CreateBuiltSolutionAsync(new(Deduplication: false), cancellationToken);

		// Act
		var diagnosis = await solution.DiagnoseAsync(cancellationToken);

		// Assert
		await Assert.That(diagnosis.ExitCode).IsEqualTo(1).Because(diagnosis.Output);
		using var report = Parse(diagnosis);
		var finding = SingleFinding(report);
		await Assert.That(finding.GetProperty("Kind").GetString()).IsEqualTo("BetweenReferences");
		await Assert.That(finding.GetProperty("Handled").GetBoolean()).IsFalse();
		await Assert
			.That(finding.GetProperty("Sources").EnumerateArray().Select(source => source.GetString() ?? string.Empty))
			.IsEquivalentTo(["Lib.A.xml", "Lib.B.xml"]);
		var duplicateKey = report
			.RootElement.GetProperty("Cache")
			.GetProperty("DuplicateKeys")
			.EnumerateArray()
			.Single();
		await Assert.That(duplicateKey.GetProperty("Id").GetString()).IsEqualTo(ConsumerSolution.InjectedTypeId);
		await Assert.That(duplicateKey.GetProperty("Count").GetInt32()).IsEqualTo(3);
	}

	[Test]
	[Timeout(Timeout)]
	public async Task Script_GivenTypeInOneReferenceAndTheProject_RecommendsTheExclude(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var solution = await CreateBuiltSolutionAsync(new(LibraryCount: 1), cancellationToken);

		// Act
		var diagnosis = await solution.DiagnoseAsync(cancellationToken);

		// Assert
		await Assert.That(diagnosis.ExitCode).IsEqualTo(1).Because(diagnosis.Output);
		using var report = Parse(diagnosis);
		var finding = SingleFinding(report);
		await Assert.That(finding.GetProperty("Kind").GetString()).IsEqualTo("ReferenceAndProject");
		await Assert.That(finding.GetProperty("Handled").GetBoolean()).IsFalse();
		await Assert
			.That(Recommendations(report))
			.Contains(recommendation =>
				recommendation.Contains(
					$"<PurviewOpenAPIXmlCommentExclude Include=\"{ConsumerSolution.InjectedTypeId}\" />",
					StringComparison.Ordinal
				)
			);
	}

	[Test]
	[Timeout(Timeout)]
	public async Task Script_GivenFixedConsumer_ReportsNothingWouldThrow(CancellationToken cancellationToken)
	{
		// Arrange
		var solution = await CreateBuiltSolutionAsync(new(), cancellationToken);

		// Act
		var diagnosis = await solution.DiagnoseAsync(cancellationToken);

		// Assert
		await Assert.That(diagnosis.ExitCode).IsEqualTo(0).Because(diagnosis.Output);
		using var report = Parse(diagnosis);
		await Assert.That(report.RootElement.GetProperty("GeneratorPresent").GetBoolean()).IsTrue();
		await Assert.That(report.RootElement.GetProperty("WouldThrow").GetBoolean()).IsFalse();
		await Assert.That(SingleFinding(report).GetProperty("Handled").GetBoolean()).IsTrue();
		await Assert
			.That(report.RootElement.GetProperty("Cache").GetProperty("DuplicateKeys").GetArrayLength())
			.IsEqualTo(0);
	}

	[Test]
	[Timeout(Timeout)]
	public async Task Script_GivenProjectWithoutOpenAPI_ReportsNoGenerator(CancellationToken cancellationToken)
	{
		// Arrange
		var solution = await CreateBuiltSolutionAsync(new(ReferencesOpenAPI: false), cancellationToken);

		// Act
		var diagnosis = await solution.DiagnoseAsync(cancellationToken);

		// Assert
		await Assert.That(diagnosis.ExitCode).IsEqualTo(0).Because(diagnosis.Output);
		using var report = Parse(diagnosis);
		await Assert.That(report.RootElement.GetProperty("GeneratorPresent").GetBoolean()).IsFalse();
		await Assert.That(report.RootElement.GetProperty("Findings").GetArrayLength()).IsEqualTo(0);
	}

	static async Task<ConsumerSolution> CreateBuiltSolutionAsync(
		ConsumerOptions options,
		CancellationToken cancellationToken
	)
	{
		var solution = await ConsumerSolution.CreateAsync(options, cancellationToken);
		var build = await solution.BuildAsync(cancellationToken);
		if (build.ExitCode != 0)
		{
			throw new InvalidOperationException($"The consumer did not build:\n{build.Output}");
		}

		return solution;
	}

	static JsonDocument Parse(ProcessResult diagnosis) => JsonDocument.Parse(diagnosis.StandardOutput);

	static JsonElement SingleFinding(JsonDocument report) =>
		report.RootElement.GetProperty("Findings").EnumerateArray().Single();

	static List<string> Recommendations(JsonDocument report) =>
		[
			.. report
				.RootElement.GetProperty("Recommendations")
				.EnumerateArray()
				.Select(item => item.GetString() ?? string.Empty),
		];
}
