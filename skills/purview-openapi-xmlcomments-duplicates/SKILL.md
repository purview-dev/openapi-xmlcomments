---
name: purview-openapi-xmlcomments-duplicates
description: "Use when an ASP.NET Core OpenAPI document fails with \"ArgumentException: An item with the same key has already been added\" from XmlCommentCache.GenerateCacheEntries, /openapi/v1.json answers 500, the build warns POAX001, or a project removes the Microsoft.AspNetCore.OpenApi.SourceGenerators analyzer to avoid duplicate XML documentation members. Finds which documentation IDs collide and where each copy comes from, then installs and configures Purview.OpenAPI.XmlComments (PurviewOpenAPIXmlCommentExclude, PurviewOpenAPIXmlCommentDeduplication) and verifies the fix."
license: MIT
metadata:
  package: Purview.OpenAPI.XmlComments
  repository: purview-dev/openapi-xmlcomments
---

# Debugging duplicate OpenAPI XML-comment members

The `Microsoft.AspNetCore.OpenApi` XML-comment source generator builds one dictionary with `Add` from two sources and
de-duplicates neither (dotnet/aspnetcore#67192):

1. every `<member>` of every **project-reference** XML documentation file (no accessibility filter - `internal`
   members count);
2. every **public**, documented symbol of the API project's own compilation, including other source generators'
   output.

A documented type compiled into more than one of those - nearly always an attribute or polyfill that a source
generator injects into every assembly - is added twice and the cache throws on first use. `Purview.OpenAPI.XmlComments`
fixes it at build time without turning the generator off.

## 1. Confirm it is this problem

It is, when the exception or log shows:

```text
System.ArgumentException: An item with the same key has already been added. Key: T:Some.Namespace.SomeType
   at System.Collections.Generic.Dictionary`2.Add(...)
   at Microsoft.AspNetCore.OpenApi.Generated.<OpenApiXmlCommentSupport_generated>...XmlCommentCache.GenerateCacheEntries()
```

The `Key:` is the documentation ID (`T:` type, `M:` method, `P:` property, `F:` field, `E:` event). A different
stack - for example a duplicate *schema* or *operation id* - is a different problem; stop here.

Also treat these as the same problem: a build warning `POAX001`, or a project file containing
`<Analyzer Remove=... Microsoft.AspNetCore.OpenApi.SourceGenerators ...>` (that workaround drops every description).

## 2. Find every colliding ID and its sources

Run the bundled [scripts/find-duplicate-documentation-ids.ps1](scripts/find-duplicate-documentation-ids.ps1)
against the project that calls `AddOpenApi()`. It asks MSBuild for exactly the files the generator reads and reports
every collision, not just the first one the exception happened to name:

```bash
pwsh scripts/find-duplicate-documentation-ids.ps1 -Project path/to/Api
# -TargetFramework net10.0 for a multi-targeted project; -Json for machine-readable output
```

Read its findings:

| Finding | Meaning | Fix |
| --- | --- | --- |
| `BetweenReferences` | The ID is in two or more referenced projects' documentation. | Installing the package fixes it automatically. |
| `ReferenceAndProject` | The ID is in exactly one referenced project and in the API itself. | An exclude (step 3). This is what `POAX001` reports. |
| Generated cache key added more than once | The **last build** throws. | Rebuild after changing the configuration; still there means the copies are not coming from referenced XML (step 5). |

The script exits `0` when nothing would throw with the current configuration, `1` when something would, `2` when it
could not inspect the project. Without `pwsh`, follow [references/diagnosing.md](references/diagnosing.md), which also
explains how to read the generated cache and a binary log by hand.

## 3. Update the configuration

Install the package in the API project (it is a development dependency and does nothing in a project without the
OpenAPI package):

```xml
<PackageReference Include="Purview.OpenAPI.XmlComments" Version="<latest>" PrivateAssets="all" />
```

Under Central Package Management add `<PackageVersion Include="Purview.OpenAPI.XmlComments" Version="<latest>" />` to
`Directory.Packages.props` and omit `Version` above, or use a `GlobalPackageReference` for every project. Use the
latest version from nuget.org; do not invent one.

For every `ReferenceAndProject` finding, add the exclude the script printed:

```xml
<ItemGroup>
	<PurviewOpenAPIXmlCommentExclude Include="T:Some.Namespace.SomeType" />
</ItemGroup>
```

Then remove any workaround that turns the generator off. Keep everything else - especially
`GenerateDocumentationFile` in the libraries, which is what produces the descriptions.

Where each setting belongs (a single API, several APIs, `Directory.Build.props`), the CPM forms, warnings-as-errors
and turning the package off are in [references/configuration.md](references/configuration.md).

## 4. Verify

1. Rebuild the API (`dotnet build`). There must be no `POAX001`.
2. Run the script again: it must report `Result: nothing would throw` and exit `0`.
3. Request `/openapi/v1.json` and check it returns `200` with the expected descriptions.

`POAX001` is found after compile, so an exclude added for it takes effect on the **next** build.

## 5. When the package does not resolve it

- **The cache still has a duplicate after a rebuild, and the script finds nothing in the referenced documentation.**
  Both copies come from the API's own compilation, or from something other than a project reference's XML file
  (for example an `AdditionalFiles` XML the project adds itself under a different shape). Find the key in the generated
  cache (see [references/diagnosing.md](references/diagnosing.md)) and remove the second declaration.
- **The duplicated ID is real API surface** - two referenced projects each declare the same public type in the same
  namespace. The package removes both copies' descriptions, which is safe but loses them; the real fix is a rename,
  because the type is also ambiguous to every consumer referencing both.
- **`POAX001` for a type that is `internal` in the API.** The generator only reads the API's public symbols, so it does
  not collide; the warning over-reports. Silence it with `<MSBuildWarningsAsMessages>POAX001</MSBuildWarningsAsMessages>`
  or add the exclude anyway (harmless).

## Do not

- Remove the generator, or set `GenerateDocumentationFile=false` in a library - both lose descriptions.
- Make the injected type `internal` or `[Embedded]` expecting it to help: the compiler still writes internal members to
  the XML documentation file, and the generator reads every member of it.
- Keep one copy of a duplicated reference ID by hand-editing XML in `bin/`: it is regenerated on every build, and the
  API's own copy would still collide.
- Report the fix as done before step 4 passes.

## For source-generator authors

The durable fix in a generator that injects a type into every compilation is to emit it **without XML documentation
comments**: an undocumented member never reaches the XML file, so it never reaches the cache. `internal` and
`[Embedded]` are still good hygiene, but on their own do not stop the collision.
