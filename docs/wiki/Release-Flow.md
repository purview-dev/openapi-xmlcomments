# Release Flow

- `package.json` is the authoritative version. `Purview.BuildSdk` reads it, so no project file carries a version.
- A pull request runs the shared `purview-dev/build` pipeline (restore, build, lint, every test tier, pack and pack
  validation) and the packed-consumer gate.
- A push to `main` runs the release action with `Release:Mode=NuGet`: it packs, publishes to nuget.org through Trusted
  Publishing, and creates the `v<version>` GitHub release, only when that tag does not already exist. Bump
  `package.json` and add a `CHANGELOG.md` entry to release.
- `.config/purview-build.json`'s `PackValidation.RequiredContent` is the exhaustive list of what the package ships;
  `just pipeline-pack-validate` runs the same validation locally.

To try a build in another repository before releasing it:

```text
just pipeline-local-release --PublishLocalNuGet:LocalFeedPath=p:/_sync-projects/.local-nuget/
```

Publish a new version each time: NuGet reuses an already-extracted package with the same id and version.
