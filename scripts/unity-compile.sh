#!/bin/bash
# Recompile the already-open Unity editor and print OK or the list of CS errors
# (exits non-zero on errors). Compile-time errors only - not runtime bugs.
. "$(dirname "$0")/_unity_env.sh"

rm -f "$RES"
date +%s%N > "$REQ"   # change mtime → watcher fires (a plain timestamp = compile only)

for _ in $(seq 1 80); do
  if [ -f "$RES" ]; then
    cat "$RES"
    grep -q '^ERRORS' "$RES" && exit 1
    exit 0
  fi
  sleep 0.25
done

echo "timeout — no result. Editor open? Interaction Mode = No Throttling?"
exit 1
