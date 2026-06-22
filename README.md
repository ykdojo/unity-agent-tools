# unity-agent-tools

Drive an already-open Unity editor from the command line so an AI coding agent (or you) can compile, enter/exit Play mode, screenshot the Game view, and set the time scale - with no port, no custom build, and no clicking around in the editor.

It works by dropping one small editor script (`CompileWatcher.cs`) into your project. That script watches a file under `Temp/`; thin shell scripts write a request and poll for the result. No networking, no auth.

## Why

When you iterate on a Unity game with an AI agent, the slow part is the human round-trip: switch to Unity, wait for the compile, click Play, watch, click Stop. These tools let the agent do all of that from the shell - and read a screenshot back - so it can verify its own changes without you in the loop.

## What you get

- `unity-compile.sh` - recompile and print `OK` or the list of C# errors (exits non-zero on errors). Compile-time errors only, not runtime bugs.
- `unity-play.sh` / `unity-play.sh stop` - compile and enter Play mode if clean / exit Play mode.
- `unity-shot.sh` - capture the Game view to `Temp/shot.png` (read it as an image).
- `unity-speed.sh N` - set `Time.timeScale` (fast-forward or slow the running game; clamped to [0.1, 20]).

## A layered way to test

Driving the editor from the shell lets an agent verify its own work. Spend the least effort that still catches the bug - climb only when the cheaper layer can't see it:

1. **Pure logic, no editor (cheapest).** Pull game math/decisions into plain, deterministic C# (pass `dt`/time in; no `Time.*` or transforms) and cover it with Unity Test Framework **EditMode** tests. Fastest, runs without Play mode; `unity-compile.sh` at least confirms it compiles.
2. **Play mode + game state (mid).** Enter Play (`unity-play.sh`), fast-forward with `unity-speed.sh`, and assert on extracted state - positions, HP, counts - instead of pixels.
3. **Screenshots (most expensive).** `unity-shot.sh` to look at the screen. Reserve for layout and visual effects - anything spatial that state can't capture.

These tools cover layer 3 (and the Play/speed plumbing for layer 2); layers 1-2 are mostly a matter of how you structure your own code.

## Requirements

- Unity with the editor open on your project.
- Two one-time editor steps (the installer reminds you):
  - Click the editor window once after install so it loads `CompileWatcher.cs`.
  - Settings > General > **Interaction Mode = "No Throttling"**, so the editor keeps ticking while unfocused. Without this the scripts time out when Unity isn't focused.
  - ("Run In Background" is enabled for you by the watcher, so the game keeps simulating and rendering for screenshots while unfocused.)

## Install

### As a Claude Code plugin

```
/plugin marketplace add ykdojo/unity-agent-tools
/plugin install unity-agent-tools@ykdojo
```

Then run the `unity-setup` skill, which finds your Unity project, runs the installer, and walks you through the one-time steps. Afterwards the `unity-run` skill compiles, plays, and screenshots the game for you.

### Manually

```
bash install.sh /path/to/your/UnityProject
```

Copies `CompileWatcher.cs` into `Assets/Editor/` and the shell scripts into `./tools/` (pass a second argument to put them elsewhere). With no project argument it walks up from the current directory looking for `ProjectSettings/ProjectVersion.txt`.

## Usage

```bash
bash tools/unity-compile.sh        # error-check only
bash tools/unity-play.sh           # compile, then enter Play if clean
bash tools/unity-speed.sh 5        # 5x fast-forward
bash tools/unity-shot.sh           # screenshot Game view -> Temp/shot.png
bash tools/unity-speed.sh 1        # back to normal
bash tools/unity-play.sh stop      # exit Play
```

The scripts auto-detect the project when run from inside it. From elsewhere, set `UNITY_PROJECT=/path/to/UnityProject`.

## How it works

`CompileWatcher.cs` loads via `[InitializeOnLoad]` and watches `Temp/compile-request`. Each shell script writes a verb to that file and polls `Temp/compile-result.txt` (or `Temp/shot.png`):

| File content | Action |
|---|---|
| a timestamp | recompile, report `OK` / CS errors |
| `play` | recompile, then enter Play mode if clean |
| `stop` | exit Play mode |
| `shot` | capture the Game view to `Temp/shot.png` |
| `speed:N` | set `Time.timeScale` to N |

Entering Play triggers a domain reload that wipes the watcher's static state, so the "enter Play after a clean compile" intent is stashed in `SessionState`.

### Caveats

- Don't fire two commands within ~1-2s of entering Play. The domain reload re-arms the file watcher, so a command sent in that window can be dropped.
- Runtime-init settings only apply on a **fresh** Play start, not a recompile-and-continue. Stop and start Play to pick them up.

## Status / testing

Run the offline smoke test any time with `bash test.sh`.

**Verified** against a live editor on an existing project:
- `install.sh` file copying and project auto-detection
- compile (clean and error-reporting), play, refusing to play when there are compile errors, stop
- screenshot (a real frame captured while Unity was unfocused) and time scale
- `CompileWatcher.cs` compiles cleanly in a real editor

**Not yet verified** (help welcome):
- A clean-room install into a brand-new Unity project - first-load behavior and the `runInBackground` auto-enable on a project where it was off.
- The Claude Code plugin distribution flow end to end (marketplace add -> install -> skills load -> `${CLAUDE_PLUGIN_ROOT}` resolves).
- Only tested on macOS so far; the scripts are plain Bash and should work on Linux, but that is unconfirmed.
