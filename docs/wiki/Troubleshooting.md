# Troubleshooting

## Find what collides

The repository ships a diagnostic script with the
[`purview-openapi-xmlcomments-duplicates`](Agent-Skills.md) skill. It asks MSBuild for exactly the documentation files
the OpenAPI generator reads, and reports every colliding ID, where each copy comes from, whether the current
configuration handles it, and what to add when it does not:

```bash
pwsh skills/purview-openapi-xmlcomments-duplicates/scripts/find-duplicate-documentation-ids.ps1 -Project path/to/Api
```

From a checkout of this repository, `just find-duplicates path/to/Api` runs the same. Options: `-TargetFramework` for a
multi-targeted project, `-Configuration`, and `-Json` for machine-readable output. It exits `0` when nothing would throw,
`1` when something would, and `2` when it could not inspect the project. Build the API first so it can also check the
API's own documentation and the generated cache (`-p:EmitCompilerGeneratedFiles=true`).

```text
Project:            src/Api/Api.csproj (net10.0)
Generator present:  True
Package installed:  True (enabled: True)
Excluded IDs:       none
Reference docs:     1 file(s)
Project docs read:  True

[NOT HANDLED] ReferenceAndProject  T:Purview.Results.GenerateResultAttribute: Domain.xml + this project

Next:
- Add to the API project (these are each in one reference and in the API itself, so they are not duplicates among the references):
<ItemGroup>
	<PurviewOpenAPIXmlCommentExclude Include="T:Purview.Results.GenerateResultAttribute" />
</ItemGroup>

Result: /openapi/v1.json would still throw.
```

| Finding | Meaning | Fix |
| --- | --- | --- |
| `BetweenReferences` | In two or more referenced projects' documentation | Install the package; automatic |
| `ReferenceAndProject` | In one referenced project and in the API itself | `PurviewOpenAPIXmlCommentExclude` (what [`POAX001`](Diagnostics.md) reports) |
| Generated cache key added more than once | The last build throws | Rebuild after changing the configuration |

## Common situations

**Still `500` after installing the package.** Rebuild: the generated cache is only regenerated on compile. If it
persists, run the script; an unhandled `ReferenceAndProject` finding needs the exclude it prints, and `POAX001` was
reported on the build that found it.

**`POAX001` keeps appearing after adding the exclude.** Check the ID matches the warning exactly and that the item is
in the API project (or a `Directory.Build.props` that covers it), not in a library.

**The script finds nothing, but the cache still has a duplicate.** Both copies come from the API's own compilation, not
from referenced documentation, so the package cannot help. Look the key up in the generated cache (each referenced
file's entries come first, the API's last) and remove the second declaration.

**The duplicated type is real API.** Two referenced projects declare the same public type in the same namespace. The
package removes both copies' descriptions, which is safe but loses them; rename one, since consumers referencing both
see an ambiguous type anyway.

**The package does not seem to run.** `dotnet msbuild path/to/Api/Api.csproj -getProperty:PurviewOpenAPIXmlCommentDeduplication`
should print `true`. A normal-verbosity build (`-v:n`) logs `OpenAPI XML comments: removed '...'` for every removal. It
only runs in a project that references `Microsoft.AspNetCore.OpenApi`.

**A workaround removes the generator.** Delete it once the script reports nothing would throw: it drops every XML
description. The script lists any project file that looks like one.

## By hand

Without `pwsh`, the generated cache shows every key:

```bash
dotnet build path/to/Api -p:EmitCompilerGeneratedFiles=true
grep -o 'cache\.Add(@"[^"]*"' path/to/Api/obj/Debug/*/generated/Microsoft.AspNetCore.OpenApi.SourceGenerators/*/OpenApiXmlCommentSupport.generated.cs | sort | uniq -d
```

and the documentation files the generator reads are

```bash
dotnet msbuild path/to/Api/Api.csproj -t:ResolveReferences -getItem:AdditionalFiles -p:PurviewOpenAPIXmlCommentDeduplication=false
```

The skill's `references/diagnosing.md` walks through classifying the result, finding which generator injects the type,
and reading a binary log.
