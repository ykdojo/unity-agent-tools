#!/bin/bash
# Shared helper: resolve the Unity project root and its Temp request/result paths.
# Source this from the other unity-*.sh scripts:  . "$(dirname "$0")/_unity_env.sh"
#
# Project resolution order:
#   1. $UNITY_PROJECT if set
#   2. walk up from the current directory for ProjectSettings/ProjectVersion.txt
#   3. search down (<=3 levels) from the scripts' parent dir for the same marker

_resolve_project() {
  if [ -n "$UNITY_PROJECT" ]; then printf '%s' "$UNITY_PROJECT"; return 0; fi
  local d="$PWD"
  while [ "$d" != "/" ]; do
    if [ -f "$d/ProjectSettings/ProjectVersion.txt" ]; then printf '%s' "$d"; return 0; fi
    d="$(dirname "$d")"
  done
  local base found
  base="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
  found="$(find "$base" -maxdepth 3 -path '*/ProjectSettings/ProjectVersion.txt' 2>/dev/null | head -1)"
  if [ -n "$found" ]; then printf '%s' "$(cd "$(dirname "$found")/.." && pwd)"; return 0; fi
  return 1
}

PROJECT="$(_resolve_project)" || {
  echo "unity-agent-tools: no Unity project found. Set UNITY_PROJECT=/path or run from inside the project." >&2
  exit 1
}
TEMP="$PROJECT/Temp"
REQ="$TEMP/compile-request"
RES="$TEMP/compile-result.txt"
SHOT="$TEMP/shot.png"

if [ ! -d "$TEMP" ]; then
  echo "unity-agent-tools: $TEMP not found - is the Unity editor open on $PROJECT?" >&2
  exit 1
fi
