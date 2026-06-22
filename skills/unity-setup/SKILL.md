---
name: unity-setup
description: Install unity-agent-tools into the current Unity project so the editor can be driven from the command line (compile, play, screenshot, speed). Use when setting up a Unity project for headless agent iteration.
---

Set up unity-agent-tools in the user's Unity project so you can compile, play, and screenshot it from the shell.

1. **Find the Unity project root** - the directory containing `Assets/` and `ProjectSettings/ProjectVersion.txt`. It may be the repo root or a subfolder. If you can't find exactly one, ask the user.

2. **Run the installer**, which copies `CompileWatcher.cs` into `Assets/Editor/` and the shell scripts into a `tools/` dir:
   ```bash
   bash "${CLAUDE_PLUGIN_ROOT}/install.sh" <unity-project-dir>
   ```
   By default the scripts land in `./tools` (current dir). Pass a second argument to put them elsewhere, e.g. the repo root.

3. **Tell the user to do the two one-time editor steps** (you cannot do these for them):
   - Click the Unity editor window once so it loads `CompileWatcher.cs`.
   - **Settings > General > Interaction Mode = "No Throttling"** so the editor keeps ticking while unfocused. Without this, the scripts time out when Unity isn't focused.
   ("Run In Background" is enabled automatically by the watcher.)

4. **Verify** once they confirm, by running `bash tools/unity-compile.sh` - it should print `OK`.

5. **Offer to document it** in the project's `CLAUDE.md` so future sessions know the workflow: `unity-compile.sh` (compile-only error check), `unity-play.sh` / `unity-play.sh stop` (enter/exit Play), `unity-shot.sh` (screenshot the Game view to `Temp/shot.png`), `unity-speed.sh N` (set `Time.timeScale`). Note the caveat: don't fire commands back-to-back instantly - the domain reload when Play starts re-arms the file watcher, so a command in that ~1-2s window can be missed.
