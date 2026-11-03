# Agent Skills

This repository authors an **agent skill** so AI coding agents can diagnose and fix duplicate OpenAPI XML-comment
members without rediscovering how the generator works. Skills are authored at `skills/<name>/SKILL.md` and installed
with the skills CLI, which clones this git repository - **no NuGet package ships them**.

```bash
npx skills add purview-dev/openapi-xmlcomments
```

## Skills published by this repository

| Skill | Covers |
| --- | --- |
| `skills/purview-openapi-xmlcomments-duplicates` | Recognising the `An item with the same key has already been added` failure and `POAX001`, finding every colliding documentation ID and its sources, installing and configuring `Purview.OpenAPI.XmlComments`, and verifying the fix |

It bundles:

- `scripts/find-duplicate-documentation-ids.ps1` - the diagnostic described in [Troubleshooting](Troubleshooting.md).
  The integration tests run it against real consumers, so the skill's instructions stay true to its output.
- `references/diagnosing.md` - the same diagnosis by hand: the generated cache, the `AdditionalFiles` query, finding
  the generator that injects the type, and binary logs.
- `references/configuration.md` - every configuration form: `PackageReference`, Central Package Management,
  `GlobalPackageReference`, excludes for one or several APIs, warnings and turning it off.

## When an agent should load it

- `/openapi/v1.json` answers `500`, or a log shows `XmlCommentCache.GenerateCacheEntries` with a duplicate key.
- A build warns `POAX001`.
- A project removes the `Microsoft.AspNetCore.OpenApi.SourceGenerators` analyzer.

## Authoring rules

- Every skill name starts with `purview-openapi-xmlcomments`: skills install into one flat folder per agent, so an
  unprefixed name collides with somebody else's.
- A skill links only inside its own folder, because only that folder is installed.
- `src/tests/Repository.UnitTests` validates every skill on every test run against the Agent Skills specification
  through `Purview.Skills.Testing`.
