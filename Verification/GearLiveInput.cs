using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class GearLiveInput
{
    public static Task<string> Run()
    {
        var sandbox = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        if (!Application.isPlaying || sandbox == null || sandbox.gameObject.scene.name != "PhaseGearDemo")
            throw new InvalidOperationException("Open PhaseGearDemo in Play mode first.");
        var result = new TaskCompletionSource<string>();
        sandbox.StartCoroutine(Check(sandbox, result));
        return result.Task;
    }
    static IEnumerator Frames(Keyboard keyboard, int count, params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        for (int i = 0; i < count; i++) yield return null;
    }
    static bool Require(bool condition, string message, TaskCompletionSource<string> result)
    {
        if (condition) return true;
        result.SetException(new InvalidOperationException(message)); return false;
    }
    static void Capture(PhaseSandbox sandbox, string name)
    {
        Directory.CreateDirectory("TestResults/GearDemo");
        File.WriteAllBytes("TestResults/GearDemo/" + name + ".png", sandbox.Frame.EncodeToPNG());
    }
    static IEnumerator Check(PhaseSandbox sandbox, TaskCompletionSource<string> result)
    {
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
            yield return null;
            keyboard = InputSystem.AddDevice<Keyboard>("PhaseGearVerification");
            var s = sandbox.Controller; s.Reset();
            int jumps = s.jumps, dashes = s.dashes, deaths = s.deaths;
            int frame = Time.frameCount; double time = s.time;
            Capture(sandbox, "start");
            yield return Frames(keyboard, 25, Key.RightArrow);
            yield return Frames(keyboard, 30, Key.RightArrow, Key.UpArrow, Key.C);
            var gear = s.gears[0]; double lift = gear.Lift;
            if (!Require(lift > 0 && gear.platform.x > gear.startX && s.phase == 0, "up climb drives the white mechanism without changing phase", result)) yield break;
            Capture(sandbox, "climbing");
            yield return Frames(keyboard, 12, Key.C);
            if (!Require(gear.Lift == lift, "hanging holds the mechanism", result)) yield break;
            yield return Frames(keyboard, 12, Key.C, Key.DownArrow);
            if (!Require(gear.Lift < lift, "down climb reverses the mechanism", result)) yield break;
            lift = gear.Lift;
            yield return Frames(keyboard, 6, Key.UpArrow);
            if (!Require(gear.Lift == lift, "release keeps the mechanism parked", result)) yield break;
            yield return Frames(keyboard, 1);
            s.Reset();
            yield return Frames(keyboard, 25, Key.RightArrow);
            yield return Frames(keyboard, 95, Key.RightArrow, Key.UpArrow, Key.C);
            if (!Require(s.gears[0].wall.y == s.gears[0].top && s.player.grounded, "reach the top stop and climb onto the wall", result)) yield break;
            Capture(sandbox, "positioned");
            yield return Frames(keyboard, 40, Key.RightArrow, Key.Z);
            yield return Frames(keyboard, 20);
            if (!Require(s.player.grounded && s.player.y + s.player.h == s.gears[0].platform.y && s.phase == 1, "first jump lands on the positioned white platform", result)) yield break;
            Capture(sandbox, "platform");
            yield return Frames(keyboard, 16, Key.RightArrow, Key.Z);
            yield return Frames(keyboard, 10, Key.RightArrow, Key.X);
            yield return Frames(keyboard, 22);
            if (!Require(s.phase == 2 && s.player.grounded && s.player.y + s.player.h == 80 && s.dashes > dashes, "jump then dash lands on green without an extra phase change", result)) yield break;
            yield return Frames(keyboard, 26, Key.RightArrow, Key.Z);
            yield return Frames(keyboard, 18);
            if (!Require(s.phase == 0 && s.player.grounded && s.player.y + s.player.h == 56, "next jump lands on red", result)) yield break;
            yield return Frames(keyboard, 26, Key.RightArrow, Key.Z);
            yield return Frames(keyboard, 18);
            if (!Require(s.deaths == deaths && s.player.grounded && s.player.x >= 296 && s.player.y + s.player.h == 32, "route reaches the resting ledge without a death", result)) yield break;
            Capture(sandbox, "finish");
            string report = $"passed: reversible drive, hold, release, endpoints, full keyboard route; frames={Time.frameCount-frame}, controllerSeconds={s.time-time:F3}, jumps={s.jumps-jumps}, dashes={s.dashes-dashes}, deaths={s.deaths-deaths}";
            File.WriteAllText("TestResults/GearDemo/live-input.txt", report);
            result.SetResult(report);
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            Time.captureDeltaTime = captureDelta;
            InputSystem.settings.backgroundBehavior = focus;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background;
        }
    }
}
