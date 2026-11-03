# Configuration

Nothing needs configuring for the common case. Everything below is MSBuild, set in the API project or in
`Directory.Build.props`.

| Setting | Default | Effect |
| --- | --- | --- |
| `<PurviewOpenAPIXmlCommentDeduplication>false</PurviewOpenAPIXmlCommentDeduplication>` | `true` | Turns the package off: neither target runs. |
| `<PurviewOpenAPIXmlCommentExclude Include="T:Some.Type" />` | none | Removes that ID from every referenced documentation file even when it occurs once. Normalised like every other ID, so a method ID may be written with or without its `~ReturnType` suffix. |

## Warnings

`POAX001` is an ordinary MSBuild warning (see [Diagnostics](Diagnostics.md)):

```xml
<PropertyGroup>
	<!-- fail the build instead -->
	<MSBuildWarningsAsErrors>$(MSBuildWarningsAsErrors);POAX001</MSBuildWarningsAsErrors>
	<!-- or silence it -->
	<MSBuildWarningsAsMessages>$(MSBuildWarningsAsMessages);POAX001</MSBuildWarningsAsMessages>
</PropertyGroup>
```

## Seeing what it did

Every removal is logged at normal verbosity:

```text
dotnet build -v:n
...
OpenAPI XML comments: removed 'T:Ambient.InjectedAttribute' from Lib.A, Lib.B (duplicate).
```

The filtered copies are in `obj/<configuration>/<tfm>/purview-openapi-xml/`. With
`<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>`, the generator's cache is in
`obj/.../generated/Microsoft.AspNetCore.OpenApi.SourceGenerators/.../OpenApiXmlCommentSupport.generated.cs`, where each
ID should now appear once.
