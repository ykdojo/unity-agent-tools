---
name: unity-run
description: Compile the latest code, enter Play mode, and screenshot the running Unity game so you can see a change working. Use when asked to run the game, confirm a change works, or observe runtime behavior.
---

Run the user's Unity game and observe it, using the unity-agent-tools scripts (in the project's `tools/` dir). If they aren't installed yet, run the `unity-setup` skill first.

1. **Compile and enter Play mode:**
   ```bash
   bash tools/unity-play.sh
   ```
   If it reports compile errors, it does NOT enter play - fix the errors first (or run `bash tools/unity-compile.sh` to see them).

2. **Let the game reach the state you care about.** To skip slow lead-up, fast-forward then drop back to normal:
   ```bash
   bash tools/unity-speed.sh 5     # 5x fast-forward
   # ...wait for the moment...
   bash tools/unity-speed.sh 1     # back to normal to observe
   ```
   Use a background wait for timed delays rather than blocking the shell.

3. **Screenshot the Game view and look at it:**
   ```bash
   bash tools/unity-shot.sh        # prints the PNG path (Temp/shot.png)
   ```
   Then read that PNG as an image. For a fleeting moment, take a burst of shots ~0.5s apart and review the frames that caught it.

4. **Stop when done:**
   ```bash
   bash tools/unity-play.sh stop
   ```

Notes:
- Requires the editor open with Interaction Mode = "No Throttling" (set during setup).
- Settings like `Application.runInBackground` only apply on a **fresh** Play start (not recompile-and-continue), so stop and start Play to pick up runtime-init changes.
- Avoid firing two commands within ~1-2s of entering Play - the domain reload re-arms the watcher and can drop a command sent in that window.
