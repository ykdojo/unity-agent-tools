using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// <summary>
/// Lets an external process (e.g. an AI coding agent) drive an already-open Unity
/// editor without a port or auth. It watches Temp/compile-request and acts on the
/// file's *content*, writing results to Temp/compile-result.txt (or Temp/shot.png).
///
///   timestamp    — recompile, report "OK ..." or the list of CS errors
///   "play"       — recompile, then enter Play mode if clean
///   "stop"       — exit Play mode
///   "replay"     — exit Play (if running), recompile, re-enter Play (one command)
///   "shot"       — capture the Game view to Temp/shot.png
///   "speed:N"    — set Time.timeScale to N (fast-forward / slow the running game)
///   "click:Name" — click the named UI Button in the running game (Play mode)
///
/// The companion shell scripts (unity-compile.sh, unity-play.sh, unity-shot.sh,
/// unity-speed.sh, unity-click.sh) just write these and poll for the result.
///
/// Two prerequisites for headless use while the editor is unfocused:
///   1. Settings > General > Interaction Mode = No Throttling (so the editor
///      keeps ticking) — this one is manual, Unity has no API for it.
///   2. Run In Background — this script enables it for you (PlayerSettings),
///      so the game keeps simulating (and rendering for screenshots) unfocused.
/// </summary>
[InitializeOnLoad]
public static class CompileWatcher
{
    static readonly string Root = Directory.GetParent(Application.dataPath).FullName;
    static readonly string TempDir = Path.Combine(Root, "Temp");
    static readonly string RequestPath = Path.Combine(TempDir, "compile-request");
    static readonly string ResultPath = Path.Combine(TempDir, "compile-result.txt");
    static readonly string ShotPath = Path.Combine(TempDir, "shot.png");
    const string PlayKey = "CompileWatcher.PlayAfterCompile";
    const string ReplayKey = "CompileWatcher.ReplayAfterStop";

    static FileSystemWatcher s_Watcher;
    static volatile bool s_Triggered;     // set from watcher thread
    static bool s_Awaiting;               // a refresh we issued is in flight
    static bool s_SawCompiling;
    static bool s_PlayAfter;               // this compile was requested with "play"
    static double s_RequestStamp;
    static readonly List<string> s_Errors = new List<string>();
    static readonly List<string> s_Warnings = new List<string>();

    static CompileWatcher()
    {
        EnableBackgroundRun();
        // Re-runs after every domain reload, so the watcher is always re-armed.
        EditorApplication.update += OnUpdate;
        CompilationPipeline.compilationStarted += OnCompilationStarted;
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyFinished;
        CompilationPipeline.compilationFinished += OnCompilationFinished;
        StartWatching();
    }

    // Keep the game simulating while the editor is unfocused, so headless
    // screenshots and background play don't freeze. Idempotent: only writes the
    // player setting the first time it's seen disabled.
    static void EnableBackgroundRun()
    {
        try { if (!Application.runInBackground) PlayerSettings.runInBackground = true; }
        catch { /* setting may be unavailable mid-reload; harmless */ }
    }

    static void StartWatching()
    {
        try
        {
            if (!Directory.Exists(TempDir)) Directory.CreateDirectory(TempDir);
            s_Watcher = new FileSystemWatcher(TempDir, "compile-request")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName
            };
            s_Watcher.Changed += (_, __) => s_Triggered = true;
            s_Watcher.Created += (_, __) => s_Triggered = true;
            s_Watcher.EnableRaisingEvents = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CompileWatcher] Could not start: {e.Message}");
        }
    }

    static void OnUpdate()
    {
        // A play-requested compile has settled (this survives the domain reload
        // that compiling/playing triggers, via SessionState). Enter play once
        // the editor is idle. Clear the flag even if already playing.
        if (SessionState.GetBool(PlayKey, false)
            && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            SessionState.SetBool(PlayKey, false);
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.EnterPlaymode();
        }

        // A "replay" request has exited Play; once the editor is idle again,
        // recompile and re-enter Play. Survives the exit-play domain reload via
        // SessionState, so it needs no external sleep between stop and play.
        if (SessionState.GetBool(ReplayKey, false)
            && !EditorApplication.isPlaying
            && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            SessionState.SetBool(ReplayKey, false);
            s_PlayAfter = true;
            BeginCompile();
            return;
        }

        if (s_Triggered && !s_Awaiting && !EditorApplication.isCompiling)
        {
            s_Triggered = false;
            string raw = ReadCommand();
            string cmd = raw.ToLowerInvariant();
            if (cmd == "stop")
            {
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                WriteStop();
                return;
            }
            if (cmd == "shot")
            {
                CaptureShot();
                return;
            }
            if (cmd == "replay")
            {
                // Exit Play (if running), then OnUpdate recompiles + re-enters
                // Play once idle. Lets one command do a fresh restart, no sleep.
                if (EditorApplication.isPlaying)
                {
                    SessionState.SetBool(ReplayKey, true);
                    EditorApplication.ExitPlaymode();
                }
                else
                {
                    s_PlayAfter = true;
                    BeginCompile();
                }
                return;
            }
            if (cmd.StartsWith("speed:"))
            {
                SetSpeed(cmd.Substring(6));
                return;
            }
            if (cmd.StartsWith("click:"))
            {
                ClickUI(raw.Substring(6).Trim()); // raw: names are case-sensitive
                return;
            }
            s_PlayAfter = (cmd == "play");
            BeginCompile();
        }

        // Nothing actually recompiled (sources already up to date): report OK.
        if (s_Awaiting && !s_SawCompiling && !EditorApplication.isCompiling
            && EditorApplication.timeSinceStartup - s_RequestStamp > 1.0)
        {
            s_Awaiting = false;
            WriteResult(upToDate: true);
        }
    }

    static void BeginCompile()
    {
        s_Errors.Clear();
        s_Warnings.Clear();
        s_Awaiting = true;
        s_SawCompiling = false;
        s_RequestStamp = EditorApplication.timeSinceStartup;

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        CompilationPipeline.RequestScriptCompilation();
    }

    static void OnCompilationStarted(object _)
    {
        if (s_Awaiting) s_SawCompiling = true;
    }

    static void OnAssemblyFinished(string assembly, CompilerMessage[] messages)
    {
        if (!s_Awaiting) return;
        foreach (var m in messages)
        {
            // m.message already includes "file(line,col): error CSxxxx: ...".
            if (m.type == CompilerMessageType.Error) s_Errors.Add(m.message);
            else if (m.type == CompilerMessageType.Warning) s_Warnings.Add(m.message);
        }
    }

    static void OnCompilationFinished(object _)
    {
        if (!s_Awaiting) return;   // fires before domain reload, so results survive
        s_Awaiting = false;
        WriteResult(upToDate: false);
    }

    static void WriteResult(bool upToDate)
    {
        var sb = new StringBuilder();
        if (s_Errors.Count == 0)
            sb.AppendLine(upToDate
                ? "OK (no recompile needed)"
                : $"OK ({s_Warnings.Count} warning(s))");
        else
            sb.AppendLine($"ERRORS: {s_Errors.Count}");

        foreach (var e in s_Errors) sb.AppendLine("  " + e);
        foreach (var w in s_Warnings) sb.AppendLine("  warning: " + w);

        try { File.WriteAllText(ResultPath, sb.ToString()); }
        catch (Exception e) { Debug.LogError($"[CompileWatcher] write failed: {e.Message}"); }

        // Clean compile that was asked to play: defer the actual EnterPlaymode to
        // OnUpdate via SessionState so it outlives the upcoming domain reload.
        if (s_PlayAfter && s_Errors.Count == 0)
        {
            s_PlayAfter = false;
            SessionState.SetBool(PlayKey, true);
        }
    }

    static string ReadCommand()
    {
        try { return File.Exists(RequestPath) ? File.ReadAllText(RequestPath).Trim() : ""; }
        catch { return ""; }
    }

    static void WriteStop()
    {
        try { File.WriteAllText(ResultPath, "OK (stopped)\n"); }
        catch (Exception e) { Debug.LogError($"[CompileWatcher] write failed: {e.Message}"); }
    }

    // Fast-forward (or slow) the running game by setting Time.timeScale. Handy
    // for reaching a late game state quickly, then dropping back to 1 to observe.
    static void SetSpeed(string arg)
    {
        float scale;
        if (!float.TryParse(arg, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out scale))
            scale = 1f;
        scale = Mathf.Clamp(scale, 0.1f, 20f);
        Time.timeScale = scale;
        try { File.WriteAllText(ResultPath, $"OK (timeScale={scale})\n"); }
        catch (Exception e) { Debug.LogError($"[CompileWatcher] write failed: {e.Message}"); }
    }

    // Click a UI Button by GameObject name (e.g. "click:Btn_Exit") by dispatching
    // a pointer-click through the event system - no screen coordinates, no window
    // focus. Only active, interactable buttons are clickable, matching what a
    // real user could press. Requires the com.unity.ugui package (present in
    // default project templates).
    static void ClickUI(string name)
    {
        string msg;
        if (!EditorApplication.isPlaying)
            msg = "ERROR: not in play mode";
        else
        {
            // Two-arg overload on purpose: it exists from 2021.3 through current
            // Unity. The warning-free one-arg overload is 6000.5+ only - using it
            // would break older editors (it broke a 6000.3 project once).
            var buttons = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            UnityEngine.UI.Button target = null;
            int matches = 0;
            foreach (var b in buttons)
                if (b.gameObject.name == name) { matches++; if (target == null) target = b; }

            if (target == null)
            {
                var names = new List<string>();
                foreach (var b in buttons) names.Add(b.gameObject.name);
                names.Sort();
                msg = $"ERROR: no Button named '{name}'. Buttons: {string.Join(", ", names)}";
            }
            else if (!target.gameObject.activeInHierarchy)
                msg = $"ERROR: '{name}' exists but is inactive";
            else if (!target.interactable)
                msg = $"ERROR: '{name}' is not interactable";
            else
            {
                var ev = new UnityEngine.EventSystems.PointerEventData(
                    UnityEngine.EventSystems.EventSystem.current);
                UnityEngine.EventSystems.ExecuteEvents.Execute(target.gameObject, ev,
                    UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                msg = matches > 1
                    ? $"OK (clicked '{name}'; {matches} matches, used first)"
                    : $"OK (clicked '{name}')";
            }
        }
        try { File.WriteAllText(ResultPath, msg + "\n"); }
        catch (Exception e) { Debug.LogError($"[CompileWatcher] click failed: {e.Message}"); }
    }

    // Capture the Game view to Temp/shot.png. The file is written a frame later,
    // so unity-shot.sh polls for the PNG itself rather than the result file.
    static void CaptureShot()
    {
        try
        {
            if (File.Exists(ShotPath)) File.Delete(ShotPath);
            ScreenCapture.CaptureScreenshot(ShotPath);
            File.WriteAllText(ResultPath, "OK (shot queued)\n");
        }
        catch (Exception e) { Debug.LogError($"[CompileWatcher] shot failed: {e.Message}"); }
    }
}
