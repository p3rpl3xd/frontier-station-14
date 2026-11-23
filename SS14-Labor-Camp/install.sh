#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 p3rpl3xd
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Copy the labor camp pack into an SS14 fork and import it from Directory.Build.targets.
# Safe to run again. A destination file whose first lines contain LABORCAMP-LOCAL is left alone.
#
# Usage: ./install.sh <fork-root> [git-ref]
# Example: ./install.sh /path/to/fork

set -euo pipefail

if [[ $# -lt 1 || $# -gt 2 || "$1" == "-h" || "$1" == "--help" ]]; then
  echo "Usage: install.sh <fork-root> [git-ref]" >&2
  exit 2
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET="$(cd "$1" && pwd)"
REF="${2:-}"
SOURCE="$ROOT"
CLEANUP=""

if [[ -n "$REF" ]]; then
  if ! git -C "$ROOT" rev-parse --verify --quiet "${REF}^{commit}" >/dev/null; then
    echo "Git ref '$REF' was not found in $ROOT. Fetch it with: git fetch --tags origin" >&2
    exit 1
  fi
  SOURCE="$(mktemp -d)"
  CLEANUP="$SOURCE"
  git -C "$ROOT" archive "$REF" | tar -x -C "$SOURCE"
fi

cleanup() {
  if [[ -n "$CLEANUP" ]]; then
    rm -rf "$CLEANUP"
  fi
}
trap cleanup EXIT

if [[ ! -f "$TARGET/Content.Server/Content.Server.csproj" ]]; then
  echo "No Content.Server project at $TARGET. Pass the root of an SS14 fork." >&2
  exit 1
fi

kept=0

copy_file() {
  local src="$1"
  local dest="$2"
  mkdir -p "$(dirname "$dest")"
  if [[ -f "$dest" ]] && grep -I -q "LABORCAMP-LOCAL" "$dest"; then
    echo "Kept local adaptation $dest"
    kept=$((kept + 1))
    return
  fi
  cp -a "$src" "$dest"
}

copy_tree() {
  local src="$1"
  local dest="$2"
  if [[ ! -d "$src" ]]; then
    echo "Missing source folder $src" >&2
    exit 1
  fi
  mkdir -p "$dest"
  local path rel
  while IFS= read -r -d '' path; do
    rel="${path#"$src"/}"
    copy_file "$path" "$dest/$rel"
  done < <(find "$src" -type f ! -path '*/.git/*' -print0)
}

echo "Copying labor camp into $TARGET"
copy_tree "$SOURCE/Content.LaborCamp.Shared" "$TARGET/Content.LaborCamp.Shared"
copy_tree "$SOURCE/Content.LaborCamp.Server" "$TARGET/Content.LaborCamp.Server"
copy_tree "$SOURCE/Content.LaborCamp.Client" "$TARGET/Content.LaborCamp.Client"
if [[ -d "$SOURCE/Content.LaborCamp.Tests" ]]; then
  copy_tree "$SOURCE/Content.LaborCamp.Tests" "$TARGET/Content.LaborCamp.Tests"
fi
copy_tree "$SOURCE/Resources/Locale/en-US/_LaborCamp" "$TARGET/Resources/Locale/en-US/_LaborCamp"
copy_tree "$SOURCE/Resources/Prototypes/_LaborCamp" "$TARGET/Resources/Prototypes/_LaborCamp"
copy_tree "$SOURCE/Resources/Textures/_LaborCamp" "$TARGET/Resources/Textures/_LaborCamp"
if [[ -d "$SOURCE/Resources/ConfigPresets/LaborCamp" ]]; then
  copy_tree "$SOURCE/Resources/ConfigPresets/LaborCamp" "$TARGET/Resources/ConfigPresets/LaborCamp"
fi

write_with_eol() {
  local file="$1"
  local tmp="$2"
  if LC_ALL=C grep -q $'\r' "$file"; then
    awk '{ printf "%s\r\n", $0 }' "$tmp" > "$file"
  else
    awk '{ printf "%s\n", $0 }' "$tmp" > "$file"
  fi
  rm -f "$tmp"
}

TARGETS="$TARGET/Directory.Build.targets"
MARKER="Content.LaborCamp.Shared/Content.LaborCamp.Install.targets"
IMPORT='  <Import Project="$(MSBuildThisFileDirectory)Content.LaborCamp.Shared/Content.LaborCamp.Install.targets" Condition="Exists('"'"'$(MSBuildThisFileDirectory)Content.LaborCamp.Shared/Content.LaborCamp.Install.targets'"'"')" />'

if [[ ! -f "$TARGETS" ]]; then
  printf '<Project>\n%s\n</Project>\n' "$IMPORT" > "$TARGETS"
  echo "Created Directory.Build.targets"
elif grep -q "$MARKER" "$TARGETS"; then
  echo "Directory.Build.targets already imports labor camp"
elif ! grep -q '</Project>' "$TARGETS"; then
  echo "Directory.Build.targets has no </Project> element. Add the labor camp Import by hand." >&2
  exit 1
else
  awk -v line="$IMPORT" '
    { sub(/\r$/, ""); lines[NR] = $0 }
    END {
      last = 0
      for (i = 1; i <= NR; i++) if (lines[i] ~ /<\/Project>/) last = i
      for (i = 1; i <= NR; i++) {
        if (i == last) print line
        print lines[i]
      }
    }
  ' "$TARGETS" > "$TARGETS.tmp"
  write_with_eol "$TARGETS" "$TARGETS.tmp"
  echo "Patched Directory.Build.targets"
fi

echo "Done. Kept $kept local adaptation(s). Rebuild the client and the server."
echo "Do not add a project reference to the labor camp folders."
