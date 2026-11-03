using System.Diagnostics;

namespace Purview.OpenAPI.XmlComments.Infra;

/// <summary>
/// The one place a test spawns a process. Every consumer is built and run through here so that no test depends on
/// the environment of the test host.
/// </summary>
/// <remarks>
/// A test run under <c>dotnet test</c> inherits MSBuild variables (<c>MSBuildExtensionsPath</c>,
/// <c>MSBUILD_EXE_PATH</c>, <c>MSBuildSDKsPath</c>) that point a child <c>dotnet build</c> at the host's MSBuild, and
/// CI variables that change SDK behaviour. Both are removed.
/// </remarks>
static class ProcessRunner
{
	/// <summary>
	/// Runs <c>dotnet <paramref name="arguments"/></c> in <paramref name="workingDirectory"/>.
	/// </summary>
	public static Task<ProcessResult> RunDotNetAsync(
		string arguments,
		string workingDirectory,
		CancellationToken cancellationToken
	) => RunAsync("dotnet", arguments, workingDirectory, cancellationToken);

	/// <summary>
	/// Runs <paramref name="fileName"/> with <paramref name="arguments"/> in <paramref name="workingDirectory"/>.
	/// </summary>
	public static async Task<ProcessResult> RunAsync(
		string fileName,
		string arguments,
		string workingDirectory,
		CancellationToken cancellationToken
	)
	{
		using Process process = new()
		{
			StartInfo = new(fileName, arguments)
			{
				WorkingDirectory = workingDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			},
		};

		var environment = process.StartInfo.Environment;
		foreach (var name in environment.Keys.ToList())
		{
			if (
				name.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase)
				|| CiEnvironmentVariables.Contains(name, StringComparer.OrdinalIgnoreCase)
			)
			{
				environment.Remove(name);
			}
		}

		environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
		environment["DOTNET_NOLOGO"] = "1";
		environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";

		process.Start();
		var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var error = process.StandardError.ReadToEndAsync(cancellationToken);
		try
		{
			await process.WaitForExitAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
			process.Kill(entireProcessTree: true);
			throw;
		}

		return new(process.ExitCode, await output, await error);
	}

	static readonly string[] CiEnvironmentVariables =
	[
		"CI",
		"GITHUB_ACTIONS",
		"GITHUB_WORKSPACE",
		"TF_BUILD",
		"BUILD_SOURCESDIRECTORY",
	];
}

/// <summary>
/// The outcome of a process run by <see cref="ProcessRunner"/>.
/// </summary>
/// <param name="ExitCode">The process's exit code.</param>
/// <param name="StandardOutput">Its standard output.</param>
/// <param name="StandardError">Its standard error.</param>
sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
	/// <summary>
	/// Gets the standard output followed by the standard error, for assertions on everything a process printed.
	/// </summary>
	public string Output => StandardOutput + StandardError;
}
