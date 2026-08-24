# Releasing Cupertino.Avalonia

Releases are created from version tags on `main`. The tag is the source of truth for the NuGet and assembly version; unversioned local builds identify themselves as `0.0.0-local`.

## One-time setup

Create a NuGet.org API key that can publish the `Cupertino.Avalonia` package. Add it to the GitHub repository as an Actions secret named `NUGET_API_KEY`.

## Prepare a release

1. Move the release entries from `Unreleased` in `CHANGELOG.md` into a versioned section such as `## [0.1.0-preview.1]`.
2. Make sure the intended commit is on `main` and its `build` workflow has passed.
3. Create and push an annotated tag whose version matches the changelog section:

```bash
git switch main
git pull --ff-only
git tag -a v0.1.0-preview.1 -m "Release v0.1.0-preview.1"
git push origin v0.1.0-preview.1
```

Tags must use `vMAJOR.MINOR.PATCH` or `vMAJOR.MINOR.PATCH-PRERELEASE`. Preview tags create GitHub prereleases; stable tags create normal releases and are marked as the latest release.

The release workflow validates the tag and changelog, verifies that the tagged commit's `build` workflow passed, builds and tests the solution again with the release version, creates NuGet and symbol packages, and prepares a draft GitHub Release. It publishes to NuGet.org before publishing the GitHub Release. A rerun reuses an existing draft and safely skips a package version already present on NuGet.org.

## Build packages without releasing

Create local validation packages by passing a version explicitly:

```bash
ALLOW_DIRTY=1 ./build/pack-nuget.sh 0.1.0-preview.1
```

Published packages must be built from a clean working tree so Source Link refers to the exact packaged sources. Omit `ALLOW_DIRTY=1` for release candidates.
