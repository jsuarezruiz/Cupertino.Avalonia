# Releasing Cupertino.Avalonia

Releases are published automatically via GitHub Actions when an annotated tag is pushed to `main`.

## Release Steps

1. **Update the Changelog**  
   Move completed changes from `[Unreleased]` in [`CHANGELOG.md`](CHANGELOG.md) into a new version section (e.g. `## [0.2.0-preview]`).

2. **Verify CI**  
   Ensure that the latest commit on `main` has passed all checks.

3. **Tag and Push**  
   Create an annotated tag matching the version and push it:

   ```bash
   git switch main
   git pull --ff-only
   git tag -a v0.2.0-preview -m "Release v0.2.0-preview"
   git push origin v0.2.0-preview
   ```

## Versioning Conventions

- **Prereleases**: `vMAJOR.MINOR.PATCH-preview` or `vMAJOR.MINOR.PATCH-beta` (creates a prerelease on GitHub and NuGet).
- **Stable**: `vMAJOR.MINOR.PATCH` (publishes a release marked as latest).

