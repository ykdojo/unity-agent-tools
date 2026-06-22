#!/bin/bash
# Compile the latest code and, if it's clean, enter Play mode. Pass "stop" to exit.
#   bash unity-play.sh        # compile + enter play
#   bash unity-play.sh stop   # exit play
#   bash unity-play.sh replay # exit play (if running), recompile, re-enter play —
#                             # one command, no manual sleep between stop and play
. "$(dirname "$0")/_unity_env.sh"

CMD="${1:-play}"   # "play" (default), "stop", or "replay"

rm -f "$RES"
printf '%s' "$CMD" > "$REQ"   # content picks the action; also changes mtime → watcher fires

# poll up to ~30s for the result (a play request compiles first)
for _ in $(seq 1 120); do
  if [ -f "$RES" ]; then
    cat "$RES"
    if grep -q '^ERRORS' "$RES"; then
      echo "compile errors — did not enter play"
      exit 1
    fi
    { [ "$CMD" = "play" ] || [ "$CMD" = "replay" ]; } && echo "entering play mode"
    exit 0
  fi
  sleep 0.25
done

echo "timeout — no result. Editor not ticking? (set Interaction Mode = No Throttling)"
exit 1
