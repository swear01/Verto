using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// In the PlatformScene probe in Play mode: Wick stands on the platform and taps jump; the platform steps up two cells and carries him,
// then a second jump brings it back down.
public static class PlatformLiveInput
{
    static async Task Frames(int count)
    {
        int target = Time.frameCount + count; double deadline = Time.realtimeSinceStartupAsDouble + 20;
        while (Time.frameCount < target && Time.realtimeSinceStartupAsDouble < deadline) await Task.Delay(16);
        if (Time.frameCount < target) throw new TimeoutException("Player loop did not advance.");
    }
    public static async Task<string> Run()
    {
        var sandbox = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        if (!Application.isPlaying || sandbox == null) throw new InvalidOperationException("Open the PlatformScene probe in Play mode.");
        bool background = Application.runInBackground; var focus = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode; Keyboard keyboard = null; float captureDelta = Time.captureDeltaTime;
        // Other Editors of this project share PlayerPrefs, so the check uses the default keys in memory only.
        var bindings = PhaseSandbox.Slots.ToArray(); PhaseSandbox.ApplyControls(PhaseSandbox.DefaultSlots);
        try
        {
            Application.runInBackground = true; Time.captureDeltaTime = (float)PhaseController.Dt;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("PlatformVerification");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(3);
            var c = sandbox.Controller; c.Reset();
            if (c.platforms.Count != 1) throw new InvalidOperationException("The scene did not collect its platform.");
            var platform = c.platforms[0]; var p = c.player;
            p.x = platform.x + 8; p.y = platform.y - p.h; p.grounded = true; p.vy = 0;
            await Frames(3);
            double startX = p.x, startY = p.y; int frame = Time.frameCount, jumps = c.jumps, deaths = c.deaths; double time = c.time;
            // Count the frames where the platform moved under Wick and he still stood on it: that is riding.
            int rode = 0; double lastY = platform.y;
            void Watch() { if (platform.y != lastY && p.grounded && p.y + p.h == platform.y) rode++; lastY = platform.y; }
            InputSystem.onAfterUpdate += Watch;
            string report;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); await Frames(1);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(40);
                string up = $"pivot={platform.pivot}, platformY={platform.y}, wickY={p.y}, grounded={p.grounded}, rodeFrames={rode}";
                if (!(c.jumps == jumps + 1 && platform.pivot == 1 && platform.y == 136 && p.grounded && p.y + p.h == 136 && p.x == startX && rode > 0 && c.deaths == deaths))
                    throw new InvalidOperationException("The jump did not raise the platform with Wick on it: " + up);
                Directory.CreateDirectory("TestResults/Platform");
                File.WriteAllBytes("TestResults/Platform/up.png", sandbox.Frame.EncodeToPNG());
                int before = rode;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); await Frames(1);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(40);
                string down = $"pivot={platform.pivot}, platformY={platform.y}, wickY={p.y}, grounded={p.grounded}, rodeFrames={rode - before}";
                if (!(c.jumps == jumps + 2 && platform.pivot == 0 && platform.y == 152 && p.grounded && p.y + p.h == 152 && c.deaths == deaths))
                    throw new InvalidOperationException("The second jump did not bring the platform back: " + down);
                File.WriteAllBytes("TestResults/Platform/down.png", sandbox.Frame.EncodeToPNG());
                report = $"passed: up [{up}]; down [{down}]; startY={startY}, frames={Time.frameCount - frame}, controllerSeconds={c.time - time:F3}";
            }
            finally { InputSystem.onAfterUpdate -= Watch; }
            return report;
        }
        finally
        {
            PhaseSandbox.ApplyControls(bindings);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = focus; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background; Time.captureDeltaTime = captureDelta;
        }
    }
}
