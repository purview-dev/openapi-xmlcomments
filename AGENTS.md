# Agent Instructions

## Purpose and authority

This repository contains `Purview.OpenAPI.XmlComments`: an MSBuild package that keeps the `Microsoft.AspNetCore.OpenApi`
XML-comment source generator from throwing `ArgumentException: An item with the same key has already been added` when
the same documentation ID comes from more than one assembly (dotnet/aspnetcore#67192).

- This file is the repository-wide source of truth for AI agents. A more-specific `AGENTS.md` in a subtree, if one is
  ever added, takes precedence for that subtree.
- `.github/copilot-instructions.md` defers to this file.
- Follow explicit user instructions first, then the nearest applicable repository instructions, then established code
  patterns.
- Operate only in this repository unless the user explicitly expands the scope.
- Never read, copy, log, or commit secrets from excluded files, environment variables, user profiles, local
  configuration, test output, or provider credentials.

## Source of truth and layout

| Path | Purpose |
| --- | --- |
| `src/XmlComments.slnx` | Canonical solution for restore, build, test and pack |
| `src/src/XmlComments` | The `netstandard2.0` task assembly (`DeduplicateOpenAPIXmlComments`, `CheckOpenAPIXmlCommentOverlap`, the deduplication rules) and the package |
| `src/src/XmlComments/Sdk` | Package-only assets: `buildTransitive/Purview.OpenAPI.XmlComments.props`/`.targets` and `README.md`, the package README |
| `src/tests/XmlComments.UnitTests` | TUnit tests of the rules and of both tasks, run in-process against a mocked `IBuildEngine` |
| `src/tests/XmlComments.IntegrationTests` | TUnit tests that build and run throwaway consumers against the real OpenAPI generator, and run the skill's diagnostic script against them |
| `src/tests/Repository.UnitTests` | `Purview.Skills.Testing` conformance for every authored skill, plus the product-prefix rule |
| `skills/purview-openapi-xmlcomments-duplicates` | The authored agent skill: `SKILL.md`, `references/`, and `scripts/find-duplicate-documentation-ids.ps1` |
| `scripts/packed-consumer`, `scripts/test-packed-consumer.ps1` | The packed-consumer gate |
| `docs/wiki` | User-facing documentation suite, aggregated by the purview-dev website |
| `Directory.Packages.props` | Centrally managed NuGet versions, and the OpenAPI versions the integration consumers use |
| `src/Directory.Build.props` / `src/Directory.Build.targets` | Solution-wide SDK, package and build behaviour |
| `global.json` | Required .NET SDK, `Purview.BuildSdk` and Microsoft.Testing.Platform selection |
| `package.json` | Authoritative repository and package version |
| `.config/purview-build.json` | Shared `Purview.Build` pipeline configuration (solution, test discovery, test filter, pack validation) |
| `Justfile` | Supported local workflow commands |

## Standard workflow

1. Read this file, inspect the working tree, and locate the implementation, tests, documentation and existing patterns
   relevant to the task.
2. Confirm behaviour from code and tests rather than relying on memory or documentation alone.
3. Make the smallest coherent change. Preserve public behaviour unless the task explicitly changes it.
4. Update tests for fixes and behaviour changes. Update documentation when public behaviour changes.
5. Run the narrowest meaningful validation first, then broader validation in proportion to risk.
6. Review the diff for unrelated edits, generated noise, compatibility risks, and missing docs or tests.

## Behavioural invariants

The public surface is MSBuild, not C#: the `PurviewOpenAPIXmlCommentDeduplication` property, the
`PurviewOpenAPIXmlCommentExclude` item, the `POAX001` warning and the `PurviewDeduplicateOpenAPIXmlComments` /
`PurviewCheckOpenAPIXmlCommentOverlap` target names. Renaming any of them is a breaking change.

- **Remove every occurrence of a duplicated ID, never all but the first.** The generator also adds the API's own
  public documented symbols, and a type injected into every assembly is usually declared by the API too, so a kept
  reference copy would still collide with it. A duplicated ID is ambient code, not API surface.
- **Judge duplicates on the generator's key.** `DocumentationId.Normalize` mirrors `XmlCommentGenerator.NormalizeDocId`
  (a method's `~ReturnType` suffix is stripped except on `op_Implicit`/`op_Explicit`). Keep it in step with the
  generator.
- **Only XML documentation files are touched.** A `<doc>` root holding `<members>`; any other `AdditionalFiles` XML, and
  any file that cannot be read or parsed, is passed through untouched rather than failing the build.
- **Never defeat incremental compilation.** A filtered copy is a compiler input, so it is written only when its content
  changes. `Build_GivenUnchangedInputs_DoesNotRecompileTheAPI` proves it.
- **Resolve paths inside targets.** `IntermediateOutputPath` is empty while package props and targets are evaluated,
  and a task resolves a relative path against the MSBuild process's working directory, so output paths are built in
  the target with `$([MSBuild]::NormalizeDirectory('$(MSBuildProjectDirectory)', ...))`.
- **Hang everything off the OpenAPI package's own target.** `PurviewDeduplicateOpenAPIXmlComments` runs
  `AfterTargets="GenerateAdditionalXmlFilesForOpenApi"`, so a project without `Microsoft.AspNetCore.OpenApi` is left
  untouched. Do not re-derive the project-reference XML list: take the `.xml` items in `AdditionalFiles`, so the package
  follows whatever the OpenAPI package adds.
- **`POAX001` is the only diagnostic, and it is a warning.** The project's own contribution is only known after
  `CoreCompile`, so the overlap check cannot fix the current build; it names the `PurviewOpenAPIXmlCommentExclude` that
  fixes the next one. It can over-report (the generator only reads the project's public symbols), which is why it is
  never an error by default.

## Task assembly rules

- `netstandard2.0` only, so it loads in `dotnet build` and in Visual Studio's .NET Framework MSBuild. Do not use APIs
  that target does not have (no `NotNullWhen`, no `IsExternalInit`-backed records or `init`).
- `Microsoft.Build.Framework` and `Microsoft.Build.Utilities.Core` are compile-time only (`PrivateAssets="all"`,
  `ExcludeAssets="runtime"`). A packed copy beside the task breaks type identity for `ITask`/`ITaskItem`, so
  `**/Microsoft.Build*` is forbidden content.
- Task classes derive from `Microsoft.Build.Utilities.Task` written out in full: the SDK's implicit
  `System.Threading.Tasks` using makes a bare `Task` ambiguous.
- Keep the rules in `XmlCommentDeduplicator` and `XmlDocumentation` and the tasks thin, so the rules are unit-tested
  without MSBuild.
- Task item parameters are `ITaskItem[]` because MSBuild binds nothing else; that is the justified `CA1819` suppression.

## Packaging rules

- A project is packable **only** when its own `.csproj` declares `<IsPackable>true</IsPackable>`. The SDK reads that
  declaration from the project file text, so never move it into `Directory.Build.props`.
- The assembly ships under `tasks/netstandard2.0/` (`BuildOutputTargetFolder=tasks`), never `lib/`, `ref/` or
  `runtimes/`, which are forbidden content. `DevelopmentDependency=true` and `SuppressDependenciesWhenPacking=true`
  keep it off consumers' output and out of their packages.
- `Sdk/**` is packed to the package root, so `Sdk/buildTransitive/*` becomes `buildTransitive/*` and `Sdk/README.md`
  becomes the package README. The props resolve the task assembly relative to themselves
  (`../tasks/netstandard2.0/Purview.OpenAPI.XmlComments.dll`); only the packed-consumer gate proves that path.
- `.config/purview-build.json`'s `PackValidation.RequiredContent` is the exhaustive declaration of what the package
  ships. Update it whenever package content changes, and verify with `just pipeline-pack-validate`.

## Testing conventions

- TUnit on Microsoft.Testing.Platform only - never xUnit, NUnit, MSTest, NSubstitute or FluentAssertions. Mock with
  `TUnit.Mocks`; `TUnit.Mocks` and `Bogus` are added by `Purview.BuildSdk`, so do not add them as `PackageReference`s.
- Keep the `*.UnitTests` / `*.IntegrationTests` naming: the SDK derives the test category from it.
- Use `--treenode-filter`, not `dotnet test --filter`.
- Test naming is `{SubjectUnderTest}_{Scenario}_{Expectation}`, one `{Class}Tests` class per class under test in the
  namespace of the class under test, explicit `// Arrange` / `// Act` / `// Assert`, and a trailing `CancellationToken`
  whenever anything in the call chain accepts one. Documentation content comes from `TestBuilder`.
- An integration consumer is written to its own temporary directory with its own `Directory.Build.props`,
  `Directory.Build.targets` and `Directory.Packages.props`, loads a private copy of the task assembly (a lingering
  MSBuild node locks the one it loaded), and is built through `ProcessRunner`, which strips the host's `MSBUILD*` and
  CI variables. Locate the task assembly through a type with no MSBuild base class: loading a task type needs MSBuild
  assemblies the test process does not have.
- `Build_GivenDeduplicationDisabled_ReproducesTheDuplicateKeyFailure` is the proof that the harness can see the bug.
  Keep it: without it, every other integration test could pass against a generator that no longer fails.
- Run `just smoke-packed-consumer` when the props, targets, packaging or a dependency version changes.
- `FindDuplicateDocumentationIdsScriptTests` runs the skill's script with `-Json` against built consumers and needs
  `pwsh` on the path. Its JSON shape (`Findings[].Kind`/`Handled`/`Sources`, `Cache.DuplicateKeys`, `WouldThrow`,
  `Recommendations`) and exit codes (`0`/`1`/`2`) are what the skill documents; change them together.

## Agent skills

Authored skills live in the repository-root `skills/` folder and are delivered by the skills CLI from git
(`npx skills add purview-dev/openapi-xmlcomments`). **Nothing packs them into the NuGet package**, and no `.agents/`
or `.claude/` copy is committed.

| Skill | Answers |
| --- | --- |
| `skills/purview-openapi-xmlcomments-duplicates/` | the duplicate-key failure and `POAX001`: diagnosing every colliding ID and its sources, configuring the package, verifying the fix |

- Every skill name carries the `purview-openapi-xmlcomments` prefix: skills install into one flat folder per agent, so
  an unprefixed name collides. `Repository.UnitTests` enforces it.
- `[InheritsTests]` on `SkillTests` is load-bearing: TUnit does not walk base types, so without it nothing is checked.
- A link from a `SKILL.md` must stay inside that skill's folder; only that folder is installed.
- Keep `SKILL.md` under 500 lines and put task-shaped detail in `references/`. The skill is part of the MSBuild
  surface's documentation: a renamed property, item, warning or target changes it in the same commit.
- The script mirrors the package's rules (`Get-NormalizedId` is `DocumentationId.Normalize`); keep them in step.

## Documentation rules

- Every public member carries XML documentation; document behaviour that is not obvious from a signature in
  `<remarks>`.
- Keep `src/src/XmlComments/Sdk/README.md`, the root `README.md`, `docs/wiki`, `AGENTS.md` and the code aligned. A
  change to a property, item, warning, target name or default changes all of them in the same commit.
- `docs/wiki` is aggregated by the purview-dev website (landing page `Getting-Started.md`). Keep `_Sidebar.md`'s order
  matching the pages that exist, and keep every page to a single top-level `#` heading.

## Build, format, test and pack commands

```text
./scripts/restore-dotnet-tools.ps1
just restore
just build
just test
just test-unit
just test-integration
just lint-check
just pack
just smoke-packed-consumer
just find-duplicates <api-project>
just pipeline-pr
just pipeline-pack-validate
```

Use `just lint-fix` (CSharpier format plus the `.slnx` GUID cleanup) to fix formatting.

## Versioning and releases

- `package.json` is the authoritative release and package version. Do not manually diverge project versions.
- Release is automatic on push to `main`: `.github/workflows/release.yml` calls the shared `purview-dev/build` release
  action with `Release:Mode=NuGet`, which packs, publishes and creates the `v<version>` GitHub release only when that tag
  does not already exist.
- `.github/workflows/pr.yml` runs the shared build pipeline for pull requests and the packed-consumer gate.
- Never create release tags or publish packages manually unless the user explicitly requests a documented recovery
  procedure.
- `.github/workflows/*` and `global.json` must stay in sync: the workflows' `dotnet-version` input has to match
  `sdk.version`, and `.config/purview-build.json` has to keep pointing at `src/XmlComments.slnx`.

## Completion checklist

- Confirm the requested behaviour and scope are satisfied, and only intended files changed.
- Review the MSBuild surface, package content and dependency implications.
- Add or update focused unit tests, and an integration test for any change to the props or targets.
- Update `Sdk/README.md`, the root `README.md`, `docs/wiki`, the skill, `CHANGELOG.md` and `AGENTS.md` when the change
  affects them.
- Run appropriate build, test, formatting, pack and packed-consumer checks in proportion to risk, and state exactly what
  ran. If a check was skipped or blocked, give the concrete reason and the remaining risk.
