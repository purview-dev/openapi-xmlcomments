# Purview OpenAPI XML Comments

[![NuGet version](https://img.shields.io/nuget/v/Purview.OpenAPI.XmlComments.svg)](https://www.nuget.org/packages/Purview.OpenAPI.XmlComments)
[![Release](https://github.com/purview-dev/openapi-xmlcomments/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/openapi-xmlcomments/actions/workflows/release.yml)

An MSBuild package that stops the `Microsoft.AspNetCore.OpenApi` XML-comment source generator from failing with
`ArgumentException: An item with the same key has already been added` when the same documentation ID comes from more
than one assembly - without turning the generator off, so every XML description stays in your OpenAPI document.

## Packages

| Package | Purpose | Targets |
| --- | --- | --- |
| [`Purview.OpenAPI.XmlComments`](src/src/XmlComments/Sdk/README.md) | Removes colliding documentation IDs from the referenced XML documentation the generator reads, before compile, and warns (`POAX001`) about any it can only see after. A development dependency with no runtime footprint. | MSBuild (`netstandard2.0` task) |

The package README is the same file that ships inside the `.nupkg` (`Sdk/README.md`), so the table links straight to
the package documentation.

## Quick start

```xml
<!-- the project that calls AddOpenApi() -->
<PackageReference Include="Purview.OpenAPI.XmlComments" Version="1.0.0" PrivateAssets="all" />
```

That is the whole change: nothing to configure for the common case, where a source generator or polyfill injects the
same documented type into two or more referenced projects. Remove any
`<Analyzer Remove="...Microsoft.AspNetCore.OpenApi.SourceGenerators" />` workaround.

When only one referenced project carries the injected type, the build says so and names the line to add:

```xml
<ItemGroup>
	<PurviewOpenAPIXmlCommentExclude Include="T:Purview.Results.GenerateResultAttribute" />
</ItemGroup>
```

## Finding what collides

The exception names only the first colliding ID. The diagnostic script lists every one, where each copy comes from,
whether your configuration already handles it, and what to add when it does not:

```bash
pwsh skills/purview-openapi-xmlcomments-duplicates/scripts/find-duplicate-documentation-ids.ps1 -Project path/to/Api
```

See [Troubleshooting](docs/wiki/Troubleshooting.md).

## Agent skill

AI coding agents get the whole procedure - recognising the failure, running the diagnostic, updating the
configuration and verifying the fix - from the `purview-openapi-xmlcomments-duplicates` skill. It is published from
this repository rather than any package:

```bash
npx skills add purview-dev/openapi-xmlcomments
```

See [Agent Skills](docs/wiki/Agent-Skills.md).

## Why it exists

The generator builds one dictionary with `Add` from every `<member>` of every project-reference XML documentation file
and every public documented symbol of the API's own compilation, and de-duplicates neither
([dotnet/aspnetcore#67192](https://github.com/dotnet/aspnetcore/issues/67192)). A type injected into every assembly
therefore arrives several times and `/openapi/v1.json` answers `500`. See
[How It Works](docs/wiki/How-It-Works.md).

## Repository

| Path | Purpose |
| --- | --- |
| `src/src/XmlComments` | The task assembly and the package (`Sdk/buildTransitive`, `Sdk/README.md`) |
| `src/tests/XmlComments.UnitTests` | The deduplication rules and both tasks, in-process |
| `src/tests/XmlComments.IntegrationTests` | Real consumers built and run against the real OpenAPI generator, and diagnosed with the skill's script |
| `src/tests/Repository.UnitTests` | The authored skill against the Agent Skills specification |
| `skills/purview-openapi-xmlcomments-duplicates` | The agent skill, its references and its diagnostic script |
| `scripts/test-packed-consumer.ps1` | The packed package, consumed through `PackageReference` alone |
| `docs/wiki` | The documentation suite the purview-dev website aggregates |

```text
just build
just test
just smoke-packed-consumer
just pipeline-pack-validate
just find-duplicates path/to/Api
```

See [Contributing](docs/wiki/Contributing.md) and [AGENTS.md](AGENTS.md).

## License

[MIT](LICENSE.md)
