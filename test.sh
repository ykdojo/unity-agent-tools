#!/bin/bash
# Offline smoke test for unity-agent-tools - checks that don't need a live Unity
# editor. The compile/play/shot/speed verbs require an open editor and are
# exercised manually (see README "Status / testing").
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
fail=0
pass() { echo "  ok: $1"; }
bad()  { echo "  FAIL: $1"; fail=1; }

echo "== JSON manifests parse =="
for f in .claude-plugin/plugin.json .claude-plugin/marketplace.json; do
  if python3 -c "import json;json.load(open('$HERE/$f'))" 2>/dev/null; then pass "$f"; else bad "$f"; fi
done

echo "== shellcheck =="
if command -v shellcheck >/dev/null; then
  if shellcheck -S warning "$HERE"/scripts/*.sh "$HERE"/install.sh; then pass "shellcheck clean"; else bad "shellcheck"; fi
else
  echo "  skip: shellcheck not installed"
fi

echo "== install.sh into a temp fake project =="
FAKE="$(mktemp -d)"
mkdir -p "$FAKE/ProjectSettings" "$FAKE/Assets"
printf 'm_EditorVersion: 2022.3.0f1\n' > "$FAKE/ProjectSettings/ProjectVersion.txt"
bash "$HERE/install.sh" "$FAKE" "$FAKE/tools" >/dev/null 2>&1
[ -f "$FAKE/Assets/Editor/CompileWatcher.cs" ] && pass "watcher copied" || bad "watcher copied"
[ -x "$FAKE/tools/unity-compile.sh" ] && pass "scripts executable" || bad "scripts executable"

echo "== env resolution: editor-closed gives a clear error =="
out="$(UNITY_PROJECT="$FAKE" bash "$HERE/scripts/unity-compile.sh" 2>&1)"; rc=$?
if [ "$rc" -ne 0 ] && printf '%s' "$out" | grep -q "not found"; then pass "clear error + nonzero exit"; else bad "editor-closed error"; fi

rm -rf "$FAKE"
echo
if [ "$fail" -eq 0 ]; then echo "ALL OFFLINE CHECKS PASSED"; else echo "SOME CHECKS FAILED"; exit 1; fi
