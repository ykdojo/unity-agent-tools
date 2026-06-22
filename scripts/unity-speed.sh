#!/bin/bash
# Set the running game's Time.timeScale (1 = normal, 5 = 5x fast-forward, etc).
#   bash unity-speed.sh 5
# Clamped to [0.1, 20] by the editor. Resets to 1 on the next fresh Play start.
. "$(dirname "$0")/_unity_env.sh"

N="${1:-1}"

rm -f "$RES"
printf 'speed:%s' "$N" > "$REQ"

for _ in $(seq 1 40); do
  if [ -f "$RES" ]; then
    cat "$RES"
    exit 0
  fi
  sleep 0.25
done

echo "timeout — no result. In play mode? editor ticking?"
exit 1
