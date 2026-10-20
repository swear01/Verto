using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Drives Assets/Scenes/Drafts/ScrollTest.unity one stage per call so the Game View can be captured at each camera stop:
// 1 run, 2 reach the wall, 3-5 climb onto steps A, B and C. Every frame checks that the view stays inside the room.
public static class ScrollLiveInput
{
    static readonly (string name, Key[] keys, Func<PhaseController, bool> done)[] Stages = {
        ("run", new[] { Key.RightArrow }, null),
        ("wall", new[] { Key.RightArrow }, c => c.player.x == 552 && c.player.grounded),
        ("step-a", new[] { Key.RightArrow, Key.UpArrow, Key.C }, c => Feet(c, 248)),
        ("step-b", new[] { Key.RightArrow, Key.UpArrow, Key.C }, c => Feet(c, 152)),
        ("step-c", new[] { Key.RightArrow, Key.UpArrow, Key.C }, c => Feet(c, 56)),
    };
    static bool Feet(PhaseController c, double y) => c.player.grounded && c.player.y + c.player.h == y;

    public static async Task<string> Run()
    {
        var sandbox = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        if (!Application.isPlaying || sandbox == null || SceneManager.GetActiveScene().name != "ScrollTest")
            throw new InvalidOperationException("Open Assets/Scenes/Drafts/ScrollTest.unity in Play mode.");
        var c = sandbox.Controller; var p = c.player;
        int stage = p.x < 100 ? 0 : p.y + p.h == 344 ? (p.x < 552 ? 1 : 2) : Feet(c, 248) ? 3 : Feet(c, 152) ? 4 : -1;
        if (stage < 0) throw new InvalidOperationException($"Route finished or lost: x={p.x}, feet={p.y + p.h}");
        var (name, keys, done) = Stages[stage];
        bool background = Application.runInBackground;
        var focus = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        float captureDelta = Time.captureDeltaTime;
        Keyboard keyboard = null;
        try
        {
            Application.runInBackground = true; Time.captureDeltaTime = (float)PhaseController.Dt;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            await Frame();
            keyboard = InputSystem.AddDevice<Keyboard>("PhaseScrollVerification");
            int deaths = c.deaths, frame = Time.frameCount; double time = c.time;
            var start = View(sandbox); var seen = new List<Vector2Int> { start };
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            for (int n = 0; done == null ? n < 150 : !done(c); n++)
            {
                if (n > 900 || c.deaths != deaths) throw new InvalidOperationException($"Stage {name} failed: x={p.x}, y={p.y}, deaths={c.deaths - deaths}");
                await Frame(); seen.Add(View(sandbox));
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            // Let the eased camera settle before the caller captures the Game View.
            for (int still = 0, n = 0; still < 20; n++)
            {
                if (n > 600) throw new InvalidOperationException("Camera did not settle after " + name);
                var before = View(sandbox); await Frame(); var after = View(sandbox); seen.Add(after);
                still = before == after ? still + 1 : 0;
            }
            var size = new Vector2Int(sandbox.Frame.width, sandbox.Frame.height); var end = View(sandbox);
            foreach (var v in seen)
                if (v.x < 0 || v.y < 0 || v.x > 640 - size.x || v.y > 360 - size.y) throw new InvalidOperationException($"View left the room at {v}");
            if (Time.frameCount <= frame || c.time <= time || c.deaths != deaths) throw new InvalidOperationException("Play did not advance cleanly");
            bool moved = stage <= 1 ? end.x > start.x && end.y == 360 - size.y : end.y < start.y;
            if (!moved) throw new InvalidOperationException($"Camera did not follow during {name}: {start} -> {end}");
            if (stage == 1 && end.x != 640 - size.x) throw new InvalidOperationException($"Run should clamp at the right edge: {end}");
            if (stage == 4 && end.y != 0) throw new InvalidOperationException($"Climb should clamp at the top: {end}");
            Directory.CreateDirectory("TestResults/ScrollTest");
            File.WriteAllBytes($"TestResults/ScrollTest/{stage + 1}-{name}.png", sandbox.Frame.EncodeToPNG());
            return $"Stage {stage + 1} {name} passed: view {start}->{end} in {size.x}x{size.y}, player ({p.x},{p.y}), frames={Time.frameCount - frame}, seconds={c.time - time:F3}";
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = focus;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Time.captureDeltaTime = captureDelta; Application.runInBackground = background;
        }
    }
    static async Task Frame() { int frame = Time.frameCount; while (Time.frameCount == frame) await Task.Delay(1); }
    // The pixel camera sits over the view centre in world space (native X, negative native Y).
    static Vector2Int View(PhaseSandbox sandbox)
    {
        var at = sandbox.pixelCamera.transform.position;
        return new Vector2Int(Mathf.RoundToInt(at.x - sandbox.Frame.width / 2f), Mathf.RoundToInt(-at.y - sandbox.Frame.height / 2f));
    }
}
