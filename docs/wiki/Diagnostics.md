# Diagnostics

## POAX001

```text
warning POAX001: 'T:Ambient.InjectedAttribute' is documented both in this project and in Lib.A.xml. If this
project's copy is public, the OpenAPI XML-comment cache throws "An item with the same key has already been added" at
run time; add <PurviewOpenAPIXmlCommentExclude Include="T:Ambient.InjectedAttribute" /> to drop the reference's copy.
```

**Cause.** The ID is documented in the API project itself and in exactly one referenced project. It is not a duplicate
among the referenced documentation, so it was not removed before compile, but the generator also adds the API's own
public documented symbols, so it will collide.

**Fix.** Add the item the warning names to the API project:

```xml
<ItemGroup>
	<PurviewOpenAPIXmlCommentExclude Include="T:Ambient.InjectedAttribute" />
</ItemGroup>
```

The overlap is only visible after the API compiles, so the exclude takes effect on the next build.

**False positives.** The generator only reads the API's *public* symbols, so an overlap with an `internal` type in the
API does not fail. The warning cannot tell the two apart from the documentation file; silence it with
`MSBuildWarningsAsMessages` if it is one of those (see [Configuration](Configuration.md)).
