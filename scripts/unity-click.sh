#!/bin/bash
# Click a named UI Button in the running game - no screen coordinates, no window
# focus, no input injection. Dispatches a pointer-click through the EventSystem.
#   bash unity-click.sh Btn_Retry
# The name is the Button's GameObject name (case-sensitive). On a miss the error
# lists every Button currently in the scene. Requires Play mode + com.unity.ugui.
. "$(dirname "$0")/_unity_env.sh"

NAME="${1:-}"
if [ -z "$NAME" ]; then
  echo "usage: bash unity-click.sh <ButtonGameObjectName>"
  exit 1
fi

rm -f "$RES"
printf 'click:%s' "$NAME" > "$REQ"

for _ in $(seq 1 40); do
  if [ -f "$RES" ]; then
    cat "$RES"
    case "$(head -1 "$RES")" in OK*) exit 0 ;; *) exit 1 ;; esac
  fi
  sleep 0.25
done

echo "timeout — no result. In play mode? editor ticking?"
exit 1
