#!/usr/bin/env bash

set -euo pipefail

if [[ $# -lt 1 || -z "$1" ]]; then
  printf 'Usage: %s <version> [output-directory]\n' "$0" >&2
  exit 64
fi

version="$1"

if [[ ! "$version" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z][0-9A-Za-z-]*(\.[0-9A-Za-z][0-9A-Za-z-]*)*)?$ ]]; then
  printf 'Version must use MAJOR.MINOR.PATCH or MAJOR.MINOR.PATCH-PRERELEASE, for example 1.0.0 or 0.1.0-preview.1.\n' >&2
  exit 64
fi

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output_directory="${2:-$repository_root/artifacts/packages/$version}"
release_notes="See the v$version release notes at https://github.com/jsuarezruiz/Cupertino.Avalonia/releases/tag/v$version."

if [[ -n "$(git -C "$repository_root" status --porcelain)" && "${ALLOW_DIRTY:-}" != "1" ]]; then
  printf 'The working tree has uncommitted changes. Commit them before packing so Source Link matches the package sources.\n' >&2
  printf 'Set ALLOW_DIRTY=1 only when creating local validation packages.\n' >&2
  exit 1
fi

mkdir -p "$output_directory"

dotnet pack "$repository_root/src/Cupertino.Avalonia/Cupertino.Avalonia.csproj" \
  --configuration Release \
  -p:Version="$version" \
  -p:PackageVersion="$version" \
  -p:PackageReleaseNotes="$release_notes" \
  -p:TreatWarningsAsErrors=true \
  --output "$output_directory"

printf 'Packages created in %s\n' "$output_directory"
