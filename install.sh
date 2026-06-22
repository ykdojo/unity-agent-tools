#!/bin/bash
# Install unity-agent-tools into a Unity project:
#   - copies the editor watcher  → <project>/Assets/Editor/CompileWatcher.cs
#   - copies the shell scripts   → <tools-dest>/  (default: ./tools)
#
# Usage:
#   bash install.sh [unity-project-dir] [tools-dest-dir]
# With no args it auto-detects the Unity project by walking up from the CWD.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"

PROJECT="${1:-}"
if [ -z "$PROJECT" ]; then
  d="$PWD"
  while [ "$d" != "/" ]; do
    if [ -f "$d/ProjectSettings/ProjectVersion.txt" ]; then PROJECT="$d"; break; fi
    d="$(dirname "$d")"
  done
fi

if [ -z "$PROJECT" ] || [ ! -f "$PROJECT/ProjectSettings/ProjectVersion.txt" ]; then
  echo "Usage: bash install.sh <unity-project-dir> [tools-dest-dir]"
  echo "  <unity-project-dir> = the folder containing Assets/ and ProjectSettings/"
  exit 1
fi

TOOLS_DEST="${2:-$PWD/tools}"

mkdir -p "$PROJECT/Assets/Editor"
cp "$HERE/editor/CompileWatcher.cs" "$PROJECT/Assets/Editor/CompileWatcher.cs"
echo "✓ CompileWatcher.cs → $PROJECT/Assets/Editor/"

mkdir -p "$TOOLS_DEST"
cp "$HERE/scripts/"*.sh "$TOOLS_DEST/"
chmod +x "$TOOLS_DEST/"*.sh
echo "✓ scripts → $TOOLS_DEST/"

cat <<EOF

Done. Next steps:
  1. Click the Unity editor window once so it loads CompileWatcher.cs.
  2. Settings > General > Interaction Mode = "No Throttling"
     (so the editor keeps ticking while unfocused — required for background use).
  3. Test it:  bash "$TOOLS_DEST/unity-compile.sh"

The scripts auto-detect this project. If you run them from elsewhere, set
UNITY_PROJECT=$PROJECT
EOF
