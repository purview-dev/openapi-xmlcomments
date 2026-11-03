# Changelog

All notable changes to this repository are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). `package.json` is the authoritative version; see
[Release Flow](docs/wiki/Release-Flow.md).

Released versions correspond to `v<version>` GitHub releases.

## 1.0.0-prerelease.1

The first prerelease.

### Added

- `Purview.OpenAPI.XmlComments`, a development-dependency MSBuild package for projects that use the
  `Microsoft.AspNetCore.OpenApi` XML-comment source generator (dotnet/aspnetcore#67192).
  - Before compile, removes every occurrence of each documentation ID that occurs more than once across the XML
    documentation files in `AdditionalFiles`, judged after the generator's own `~ReturnType` normalisation, and swaps
    filtered copies in. A copy is only rewritten when its content changes, so incremental builds still skip compile.
  - `PurviewOpenAPIXmlCommentExclude` items remove an ID even when it occurs once.
  - After compile, `POAX001` warns about an ID documented both in the project and in a reference, naming the exclude
    to add.
  - `PurviewOpenAPIXmlCommentDeduplication=false` turns it off.
- The `purview-openapi-xmlcomments-duplicates` agent skill (`npx skills add purview-dev/openapi-xmlcomments`): recognising
  the duplicate-key failure and `POAX001`, diagnosing every colliding ID and its sources, configuring the package, and
  verifying the fix. It bundles `find-duplicate-documentation-ids.ps1` (also `just find-duplicates <project>`), which
  reports each collision, whether the current configuration handles it, and the configuration to add, with `-Json`
  output and an exit code for CI.
