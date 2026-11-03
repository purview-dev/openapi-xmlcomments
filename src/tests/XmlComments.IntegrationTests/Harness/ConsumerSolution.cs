using System.Text.RegularExpressions;
using Purview.OpenAPI.XmlComments.Infra;

namespace Purview.OpenAPI.XmlComments.Harness;

/// <summary>
/// What a <see cref="ConsumerSolution"/> is made of.
/// </summary>
/// <param name="TargetFramework">The consumer's target framework, <c>net10.0</c> or <c>net11.0</c>.</param>
/// <param name="LibraryCount">How many class libraries the API references. Each is <c>Lib.A</c>, <c>Lib.B</c>, ...</param>
/// <param name="Deduplication">The value of <c>PurviewOpenAPIXmlCommentDeduplication</c>; <see langword="null"/> leaves the default.</param>
/// <param name="ExcludedIds">The <c>PurviewOpenAPIXmlCommentExclude</c> items the API declares.</param>
/// <param name="ReferencesOpenAPI">Whether the API references Microsoft.AspNetCore.OpenApi at all.</param>
sealed record ConsumerOptions(
	string TargetFramework = "net10.0",
	int LibraryCount = 2,
	bool? Deduplication = null,
	IReadOnlyList<string>? ExcludedIds = null,
	bool ReferencesOpenAPI = true
);

/// <summary>
/// A throwaway consumer solution: an API and <see cref="ConsumerOptions.LibraryCount"/> class libraries, every one of
/// which compiles <see cref="InjectedTypeId"/> - a documented public attribute standing in for a type a source
/// generator or polyfill injects into every assembly.
/// </summary>
/// <remarks>
/// It is written to its own temporary directory with its own <c>Directory.Build.props</c>,
/// <c>Directory.Build.targets</c> and <c>Directory.Packages.props</c>, so nothing from this repository or the machine
/// leaks in. The API imports the package's props and targets from source, and loads a private copy of the task
/// assembly so a lingering MSBuild node cannot lock the test's own output.
/// </remarks>
sealed partial class ConsumerSolution
{
	/// <summary>
	/// The documentation ID of the attribute compiled into every consumer assembly.
	/// </summary>
	public const string InjectedTypeId = "T:Ambient.InjectedAttribute";

	ConsumerSolution(string root, ConsumerOptions options)
	{
		Root = root;
		Options = options;
	}

	/// <summary>
	/// Gets the solution's root directory.
	/// </summary>
	public string Root { get; }

	/// <summary>
	/// Gets what the solution is made of.
	/// </summary>
	public ConsumerOptions Options { get; }

	/// <summary>
	/// Gets the API project's directory.
	/// </summary>
	public string APIDirectory => Path.Combine(Root, "Api");

	/// <summary>
	/// Gets the API's intermediate output directory.
	/// </summary>
	public string APIIntermediateDirectory => Path.Combine(APIDirectory, "obj", "Debug", Options.TargetFramework);

	/// <summary>
	/// Gets the names of the libraries the API references, <c>A</c>, <c>B</c>, ...
	/// </summary>
	public IReadOnlyList<string> Libraries =>
		[.. Enumerable.Range(0, Options.LibraryCount).Select(i => ((char)('A' + i)).ToString())];

	/// <summary>
	/// Writes a new consumer solution to a new temporary directory.
	/// </summary>
	public static async Task<ConsumerSolution> CreateAsync(ConsumerOptions options, CancellationToken cancellationToken)
	{
		var root = Directory.CreateTempSubdirectory("purview-openapi-xmlcomments-it-").FullName;
		ConsumerSolution solution = new(root, options);
		await solution.WriteAsync(cancellationToken);

		return solution;
	}

	/// <summary>
	/// Builds the API, and with it every library.
	/// </summary>
	public Task<ProcessResult> BuildAsync(CancellationToken cancellationToken) =>
		ProcessRunner.RunDotNetAsync("build Api/Api.csproj -nodeReuse:false", Root, cancellationToken);

	/// <summary>
	/// Runs the built API, which serves <c>/openapi/v1.json</c> to itself and exits <c>0</c> only on a success status.
	/// </summary>
	public Task<ProcessResult> RunAPIAsync(CancellationToken cancellationToken) =>
		ProcessRunner.RunDotNetAsync($"bin/Debug/{Options.TargetFramework}/Api.dll", APIDirectory, cancellationToken);

	/// <summary>
	/// Runs the skill's <c>find-duplicate-documentation-ids.ps1</c> against the API with <c>-Json</c>.
	/// </summary>
	public Task<ProcessResult> DiagnoseAsync(CancellationToken cancellationToken) =>
		ProcessRunner.RunAsync(
			"pwsh",
			$"-NoProfile -NonInteractive -File \"{ConsumerConstants.SkillScript}\" -Project \"{APIDirectory}\" -Json",
			Root,
			cancellationToken
		);

	/// <summary>
	/// Counts the entries the generated XML-comment cache adds for <paramref name="id"/>.
	/// </summary>
	public async Task<int> CountCacheEntriesAsync(string id, CancellationToken cancellationToken)
	{
		var generated = Directory
			.EnumerateFiles(
				APIIntermediateDirectory,
				"OpenApiXmlCommentSupport.generated.cs",
				SearchOption.AllDirectories
			)
			.Single();
		var source = await File.ReadAllTextAsync(generated, cancellationToken);

		return Regex.Count(source, $@"cache\.Add\(@""{Regex.Escape(id)}""");
	}

	async Task WriteAsync(CancellationToken cancellationToken)
	{
		await WriteFileAsync(
			"Directory.Build.props",
			$"""
			<Project>
				<PropertyGroup>
					<TargetFramework>{Options.TargetFramework}</TargetFramework>
					<Nullable>enable</Nullable>
					<ImplicitUsings>enable</ImplicitUsings>
					<GenerateDocumentationFile>true</GenerateDocumentationFile>
					<NoWarn>$(NoWarn);CS1591</NoWarn>
					<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
				</PropertyGroup>
			</Project>
			""",
			cancellationToken
		);
		await WriteFileAsync("Directory.Build.targets", "<Project />", cancellationToken);
		await WriteFileAsync(
			"Directory.Packages.props",
			"""
			<Project>
				<PropertyGroup>
					<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
				</PropertyGroup>
			</Project>
			""",
			cancellationToken
		);
		await WriteFileAsync(
			"Ambient.cs",
			"""
			namespace Ambient;

			/// <summary>An attribute a source generator would inject into every assembly.</summary>
			[System.AttributeUsage(System.AttributeTargets.Class)]
			public sealed class InjectedAttribute : System.Attribute { }
			""",
			cancellationToken
		);

		foreach (var library in Libraries)
		{
			await WriteFileAsync(
				$"Lib.{library}/Lib.{library}.csproj",
				"""
				<Project Sdk="Microsoft.NET.Sdk">
					<ItemGroup>
						<Compile Include="../Ambient.cs" />
					</ItemGroup>
				</Project>
				""",
				cancellationToken
			);
			await WriteFileAsync(
				$"Lib.{library}/Widget{library}.cs",
				$$"""
				namespace Lib.{{library}};

				/// <summary>Widget from library {{library}}.</summary>
				public sealed class Widget{{library}}
				{
					/// <summary>The name of the widget from library {{library}}.</summary>
					public string Name { get; set; } = "{{library}}";
				}
				""",
				cancellationToken
			);
		}

		await WriteAPIAsync(cancellationToken);
	}

	async Task WriteAPIAsync(CancellationToken cancellationToken)
	{
		// A private copy: MSBuild keeps a task assembly loaded for as long as its node lives. Located through a type with
		// no MSBuild base class, because loading a task type needs the MSBuild assemblies this process does not have.
		var builtTaskAssembly = typeof(XmlCommentDeduplicator).Assembly.Location;
		var taskAssembly = Path.Combine(Root, "tasks", Path.GetFileName(builtTaskAssembly));
		Directory.CreateDirectory(Path.GetDirectoryName(taskAssembly)!);
		File.Copy(builtTaskAssembly, taskAssembly);

		var openAPIVersion =
			Options.TargetFramework == "net11.0"
				? ConsumerConstants.OpenAPINet11Version
				: ConsumerConstants.OpenAPINet10Version;
		var deduplication = Options.Deduplication is { } enabled
			? $"<PurviewOpenAPIXmlCommentDeduplication>{(enabled ? "true" : "false")}</PurviewOpenAPIXmlCommentDeduplication>"
			: string.Empty;
		var openAPIReference = Options.ReferencesOpenAPI
			? $"""<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="{openAPIVersion}" />"""
			: string.Empty;
		var libraryReferences = string.Concat(
			Libraries.Select(library => $"""<ProjectReference Include="../Lib.{library}/Lib.{library}.csproj" />""")
		);
		var excludes = string.Concat(
			(Options.ExcludedIds ?? []).Select(id => $"""<PurviewOpenAPIXmlCommentExclude Include="{id}" />""")
		);

		await WriteFileAsync(
			"Api/Api.csproj",
			$"""
			<Project Sdk="Microsoft.NET.Sdk.Web">
				<PropertyGroup>
					<PurviewOpenAPIXmlCommentsTaskAssembly>{taskAssembly}</PurviewOpenAPIXmlCommentsTaskAssembly>
					{deduplication}
				</PropertyGroup>
				<Import Project="{ConsumerConstants.SdkDirectory}/buildTransitive/Purview.OpenAPI.XmlComments.props" />
				<ItemGroup>
					{openAPIReference}
					{libraryReferences}
					{excludes}
					<Compile Include="../Ambient.cs" />
				</ItemGroup>
				<Import Project="{ConsumerConstants.SdkDirectory}/buildTransitive/Purview.OpenAPI.XmlComments.targets" />
			</Project>
			""",
			cancellationToken
		);

		var endpoints = string.Concat(
			Libraries.Select(library =>
				$"""

					app.MapGet("/{library}", () => new Lib.{library}.Widget{library}());
					"""
			)
		);
		var program = Options.ReferencesOpenAPI
			? $$"""
				using Microsoft.AspNetCore.Hosting.Server;
				using Microsoft.AspNetCore.Hosting.Server.Features;

				var builder = WebApplication.CreateBuilder(args);
				builder.WebHost.UseUrls("http://127.0.0.1:0");
				builder.Services.AddOpenApi();

				var app = builder.Build();
				app.MapOpenApi();{{endpoints}}

				await app.StartAsync();
				var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
				using var client = new HttpClient { BaseAddress = new Uri(address) };
				using var response = await client.GetAsync("/openapi/v1.json");
				Console.WriteLine($"STATUS {(int)response.StatusCode}");
				Console.WriteLine(await response.Content.ReadAsStringAsync());
				await app.StopAsync();

				return response.IsSuccessStatusCode ? 0 : 1;
				"""
			: """
				var app = WebApplication.CreateBuilder(args).Build();
				app.MapGet("/", () => "ok");
				return 0;
				""";
		await WriteFileAsync("Api/Program.cs", program, cancellationToken);
	}

	async Task WriteFileAsync(string relativePath, string content, CancellationToken cancellationToken)
	{
		var path = Path.Combine(Root, relativePath);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		await File.WriteAllTextAsync(path, content, cancellationToken);
	}
}
