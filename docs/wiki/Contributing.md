# Contributing

1. Read [AGENTS.md](https://github.com/purview-dev/openapi-xmlcomments/blob/main/AGENTS.md): it lists the behavioural
   invariants and the packaging rules.
2. Restore the local tools: `./scripts/restore-dotnet-tools.ps1`.
3. Make the change with its tests. A change to the props or targets needs an integration test; a change to the skill's
   script needs a `FindDuplicateDocumentationIdsScriptTests` case.
4. Run `just lint-fix`, `just test` and, for anything touching packaging, `just smoke-packed-consumer` and
   `just pipeline-pack-validate`.
5. Use Conventional Commits (`feat:`, `fix:`, `docs:`, ...); `lefthook` and `commitlint` check them.

When the MSBuild surface changes - a property, item, target name, warning or default - update the package README
(`src/src/XmlComments/Sdk/README.md`), the root `README.md`, these pages, the skill
(`skills/purview-openapi-xmlcomments-duplicates`, including its `references/` and script), `CHANGELOG.md` and
`AGENTS.md` in the same commit.

## Skills

The authored skill lives in `skills/`. Keep `SKILL.md` under 500 lines with detail in `references/`, link only inside
the skill's own folder, and keep the `purview-openapi-xmlcomments` prefix; `Repository.UnitTests` enforces the
specification. See [Agent Skills](Agent-Skills.md).
