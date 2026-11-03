# Diagnosing duplicate documentation IDs by hand

Use this when `scripts/find-duplicate-documentation-ids.ps1` cannot run (no `pwsh`), or to confirm what it reported.

## 1. See the generated cache

Build the API with the generator's output written to disk:

```bash
dotnet build path/to/Api -p:EmitCompilerGeneratedFiles=true
```

The cache is
`obj/<Configuration>/<tfm>/generated/Microsoft.AspNetCore.OpenApi.SourceGenerators/Microsoft.AspNetCore.OpenApi.SourceGenerators.XmlCommentGenerator/OpenApiXmlCommentSupport.generated.cs`.
Each entry is one line:

```csharp
cache.Add(@"T:Some.Namespace.SomeType", new XmlComment(@"The summary.", ...));
```

List every key added more than once:

```bash
# bash
grep -o 'cache\.Add(@"[^"]*"' OpenApiXmlCommentSupport.generated.cs | sort | uniq -d
```

```powershell
# PowerShell
Select-String -Path OpenApiXmlCommentSupport.generated.cs -Pattern 'cache\.Add\(@"([^"]*)"' -AllMatches |
	ForEach-Object { $_.Matches.Groups[1].Value } | Group-Object | Where-Object Count -gt 1
```

The entries appear in source order: one block per referenced documentation file, then a final block for the API's own
compilation. A key whose last copy is in the final block is one the API itself declares.

## 2. See exactly which documentation files the generator reads

The OpenAPI package's `GenerateAdditionalXmlFilesForOpenApi` target adds them to `AdditionalFiles` after
`ResolveReferences`. Ask MSBuild for them (with the package's own filtering off, to see the raw inputs):

```bash
dotnet msbuild path/to/Api/Api.csproj -t:ResolveReferences -getItem:AdditionalFiles -p:PurviewOpenAPIXmlCommentDeduplication=false
```

Only `.xml` items from project references are documentation files; each is `<ProjectRef>/bin/<Configuration>/<tfm>/<Assembly>.xml`.
Package references' documentation is **not** read, so a package cannot be a source.

Find which of them declare a given ID:

```bash
grep -l 'name="T:Some.Namespace.SomeType"' path/to/*/bin/Debug/net10.0/*.xml
```

The API's own documentation file (`obj/<Configuration>/<tfm>/<Api>.xml`) shows whether the API declares it too.

## 3. Classify

| The ID is in | Source of the collision | Fix |
| --- | --- | --- |
| two or more reference XML files | references among themselves | install the package; automatic |
| one reference XML file and the API | the reference and the API's own compilation | install the package and add `PurviewOpenAPIXmlCommentExclude` |
| only the API, twice | the API's compilation alone | not a reference problem: find and remove the second declaration |

Two IDs that differ only in a method's `~ReturnType` suffix are the same key: the generator strips the suffix (except
on `op_Implicit`/`op_Explicit`) before adding.

## 4. Find who injects the type

The colliding type is usually not in anyone's source. Look for it in the generator output of a referenced project:

```bash
dotnet build path/to/Lib -p:EmitCompilerGeneratedFiles=true
grep -rl 'class SomeType' path/to/Lib/obj/Debug/*/generated/
```

The folder name under `generated/` is the generator that emitted it. Typical culprits: opt-in attributes a generator
adds to every compilation (`Purview.Results.GenerateResultAttribute`), polyfills (`IsExternalInit`,
`RequiredMemberAttribute`, nullable attributes) carrying doc comments, and shared files linked into several projects
with `<Compile Include="..\Shared\X.cs" />`.

## 5. Use a binary log when the evidence disagrees

```bash
dotnet build path/to/Api -bl -p:EmitCompilerGeneratedFiles=true
```

Open `msbuild.binlog` in the MSBuild Structured Log Viewer and search for:

- `GenerateAdditionalXmlFilesForOpenApi` - the `AdditionalFiles` it added;
- `PurviewDeduplicateOpenAPIXmlComments` - the `OpenAPI XML comments: removed '...' from ... (duplicate|excluded)`
  messages, and the filtered copies swapped in from `obj/.../purview-openapi-xml/`;
- `Csc` - the `AdditionalFiles` the compiler actually received.

If `PurviewDeduplicateOpenAPIXmlComments` is absent, the package is not imported into that project (check the
`PackageReference`, and that `PurviewOpenAPIXmlCommentDeduplication` is not `false`).
