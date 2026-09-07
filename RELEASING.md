# Releasing Cupertino.Avalonia

Create releases from version tags on `main`. The tag sets the NuGet package and assembly version. Local builds without a version use `0.0.0-local`.

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

The release workflow checks the tag, changelog and successful CI build, then rebuilds and tests the solution with the release version. It creates NuGet and symbol packages, prepares a draft GitHub Release, and publishes to NuGet.org before publishing the release.

Rerun a failed release from the same tag and commit. The workflow reuses the draft and skips package versions already on NuGet.org. Published NuGet versions cannot be replaced, even if unlisted; code changes need a new version and tag.

You can replace assets in a GitHub draft. Published assets cannot be replaced when release immutability is enabled. See the [NuGet publishing rules](https://learn.microsoft.com/en-us/nuget/api/package-publish-resource) and [GitHub release rules](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository).

## Build packages without releasing

Create local validation packages by passing a version explicitly:

```bash
ALLOW_DIRTY=1 ./build/pack-nuget.sh 0.1.0-preview.1
```

Published packages must be built from a clean working tree so Source Link refers to the exact packaged sources. Omit `ALLOW_DIRTY=1` for release candidates.
