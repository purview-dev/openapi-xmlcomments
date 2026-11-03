# How It Works

## The generator's cache

`Microsoft.AspNetCore.OpenApi` ships a source generator that turns XML documentation into OpenAPI descriptions. It
emits a cache, built on first use, with one `Dictionary<string, XmlComment>.Add` per documentation ID, from two
sources:

1. **Project-reference documentation.** The package's `GenerateAdditionalXmlFilesForOpenApi` target adds the XML
   documentation file of each project reference to `AdditionalFiles`, and the generator adds every `<member>` in each
   one. There is no accessibility filter: `internal` members are in those files too.
2. **The API's own compilation.** Every public, documented symbol - including the output of other source generators,
   whose post-initialization sources are visible to it.

Neither is de-duplicated ([dotnet/aspnetcore#67192](https://github.com/dotnet/aspnetcore/issues/67192)). A type injected
into every assembly is added once per referenced project and once more from the API, and the second `Add` throws when
the first schema is transformed.

## What the package changes

```text
ResolveReferences
  GenerateAdditionalXmlFilesForOpenApi        (Microsoft.AspNetCore.OpenApi: adds project-reference XML)
    PurviewDeduplicateOpenAPIXmlComments      (this package: filters it)
CoreCompile                                    (the generator runs here, on the filtered copies)
  PurviewCheckOpenAPIXmlCommentOverlap        (this package: POAX001)
```

**Before compile**, `PurviewDeduplicateOpenAPIXmlComments`:

1. reads every `.xml` item in `AdditionalFiles` that is an XML documentation file (a `<doc>` root holding `<members>`);
   anything else, or anything unreadable, is passed through untouched;
2. keys each `<member>` the way the generator does, stripping a method's `~ReturnType` suffix except on conversion
   operators;
3. selects every ID that occurs more than once, plus every `PurviewOpenAPIXmlCommentExclude` ID;
4. removes **every** occurrence of each, writes a filtered copy of each file it changed to
   `obj/<configuration>/<tfm>/purview-openapi-xml/`, and swaps the copies into `AdditionalFiles`.

Every occurrence goes, not all but the first, because the API normally declares the injected type as well: its own
copy is then the only one left, and keeping a reference's copy would still collide with it. A duplicated ID is ambient
code, not API surface, so no endpoint or schema description is lost. A copy is only rewritten when its content
changes, so a no-change build still skips `CoreCompile`.

**After compile**, `PurviewCheckOpenAPIXmlCommentOverlap` compares the API's fresh documentation file with the filtered
reference documentation and reports [`POAX001`](Diagnostics.md) for any ID still in both - the case where only one
referenced project carries the type, so step 3 never saw a duplicate.

## What it cannot do

- See the API's own symbols before they are compiled. That is why `POAX001` exists, and why its fix applies to the next
  build rather than the current one.
- Fix a collision that never passes through `AdditionalFiles`.
