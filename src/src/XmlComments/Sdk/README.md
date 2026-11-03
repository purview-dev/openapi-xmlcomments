# Purview.OpenAPI.XmlComments

Stops the `Microsoft.AspNetCore.OpenApi` XML-comment source generator from failing at run time with

```text
System.ArgumentException: An item with the same key has already been added. Key: T:Some.Namespace.SomeType
   at ...XmlCommentCache.GenerateCacheEntries()
```

when the same documentation ID comes from more than one assembly, without turning the generator off, so every XML
description stays in your OpenAPI document.

## The problem

The generator builds one `Dictionary<string, XmlComment>` with `Add` from two sources, and de-duplicates neither
([dotnet/aspnetcore#67192](https://github.com/dotnet/aspnetcore/issues/67192)):

1. every `<member>` in the XML documentation file of each **project reference**, with no accessibility filter;
2. every **public**, documented symbol of the API project's own compilation, including other source generators'
   output.

A type that a source generator or polyfill injects into every assembly, for example an opt-in attribute such as
`Purview.Results.GenerateResultAttribute`, therefore arrives once per referenced project and once more from the API
itself. `/openapi/v1.json` then answers `500`. The usual workaround, removing the generator, also removes every
description from the document.

## Install

Reference the package from the project that calls `AddOpenApi()`:

```xml
<PackageReference Include="Purview.OpenAPI.XmlComments" Version="1.0.0" PrivateAssets="all" />
```

Or reference it once for every project, since it does nothing in a project without `Microsoft.AspNetCore.OpenApi`:

```xml
<!-- Directory.Packages.props -->
<GlobalPackageReference Include="Purview.OpenAPI.XmlComments" Version="1.0.0" />
```

It is a development dependency: nothing is added to your build output, and nothing flows to packages you produce.
Remove any `<Analyzer Remove="...Microsoft.AspNetCore.OpenApi.SourceGenerators" />` workaround.

## What it does

Before the API compiles, right after the OpenAPI package adds the project-reference documentation files to
`AdditionalFiles`, it:

- reads every XML documentation file in `AdditionalFiles`;
- finds each documentation ID that occurs more than once, judged after the same `~ReturnType` normalisation the
  generator applies, and each ID listed in `PurviewOpenAPIXmlCommentExclude`;
- removes **every** occurrence of those IDs, writes filtered copies to `obj/<configuration>/<tfm>/purview-openapi-xml/`,
  and swaps the copies into `AdditionalFiles`.

It removes every occurrence, not all but the first, because the API usually declares the same injected type too: the
API's own copy is then the only one left. A duplicated ID is ambient code (an injected attribute, a polyfill), not API
surface, so no endpoint or schema description is lost. A filtered copy is only rewritten when its content changes, so
incremental builds still skip compilation.

The project's own contribution is only known once it compiles. After compile, an ID still documented both in the API
and in one reference raises **`POAX001`**:

```text
warning POAX001: 'T:Ambient.InjectedAttribute' is documented both in this project and in Lib.A.xml. If this
project's copy is public, the OpenAPI XML-comment cache throws "An item with the same key has already been added" at
run time; add <PurviewOpenAPIXmlCommentExclude Include="T:Ambient.InjectedAttribute" /> to drop the reference's copy.
```

This happens when only **one** referenced project carries the injected type, because with two or more it is already a
duplicate. Add the exclude it names. `POAX001` can over-report: an overlap only fails when the API's own copy is
public.

## Configuration

| Setting | Default | Effect |
| --- | --- | --- |
| `<PurviewOpenAPIXmlCommentDeduplication>false</PurviewOpenAPIXmlCommentDeduplication>` | `true` | Turns the package off. |
| `<PurviewOpenAPIXmlCommentExclude Include="T:Some.Type" />` | none | Always removes that ID from the referenced documentation, even when it occurs once. Normalised like every other ID. |

`POAX001` is an ordinary MSBuild warning: `<MSBuildWarningsAsErrors>POAX001</MSBuildWarningsAsErrors>` promotes it,
and `<MSBuildWarningsAsMessages>POAX001</MSBuildWarningsAsMessages>` silences it.

The details of every decision are logged at normal verbosity, for example
`OpenAPI XML comments: removed 'T:Ambient.InjectedAttribute' from Lib.A, Lib.B (duplicate).`

## Troubleshooting

The exception names only the first colliding ID. A diagnostic script in the repository lists every one, where each
copy comes from, whether the current configuration handles it, and the configuration to add when it does not:

```bash
# from skills/purview-openapi-xmlcomments-duplicates/scripts/ in the repository, or the installed skill's scripts/ folder
pwsh find-duplicate-documentation-ids.ps1 -Project path/to/Api
```

It is not in the package. It ships with the `purview-openapi-xmlcomments-duplicates` agent skill, which also gives AI coding agents the full
diagnose-configure-verify procedure:

```bash
npx skills add purview-dev/openapi-xmlcomments
```

See <https://purview.dev/docs/openapi-xmlcomments/> (Troubleshooting) for the script's output and common situations.

## Limitations

- It filters only what reaches the generator as XML documentation files. Two public types with the same ID declared in
  the API project itself are a compiler problem (`CS0101`), not this one.
- `POAX001` is reported after compile, so an overlap it finds is fixed by the exclude on the next build, not the
  current one.
- It works around a generator defect. Once the generator de-duplicates its cache itself (dotnet/aspnetcore#67192), the
  package can be removed.

## More

Documentation: <https://purview.dev/docs/openapi-xmlcomments/>. Source and issues:
<https://github.com/purview-dev/openapi-xmlcomments>.
