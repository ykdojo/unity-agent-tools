#!/bin/bash
# Capture the running editor's Game view to Temp/shot.png and print its path.
# Needs the editor in Play mode (with Run In Background on, which the watcher sets).
. "$(dirname "$0")/_unity_env.sh"

rm -f "$SHOT"
printf 'shot' > "$REQ"   # content "shot" → watcher captures the Game view

# poll up to ~20s for the PNG to appear and be non-empty
for _ in $(seq 1 80); do
  if [ -f "$SHOT" ] && [ -s "$SHOT" ]; then
    sleep 0.4   # let the file write settle
    echo "$SHOT"
    exit 0
  fi
  sleep 0.25
done

echo "timeout — no screenshot. In play mode? editor ticking (No Throttling)?"
exit 1
