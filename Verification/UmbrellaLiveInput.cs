using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Plays PhaseUmbrella with the virtual keyboard: hold right and jump at the intended spots, without dying, out the right edge.
// Saves a frame per screen to TestResults/Umbrella/.
public static class UmbrellaLiveInput
{
    static readonly double[] Jumps = { 80, 142, 204, 266, 400, 462, 590, 652, 780, 852, 916, 1000, 1062, 1180 };

    public static Task<string> Run()
    {
        var sandbox = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        if (sandbox == null || !Application.isPlaying || sandbox.gameObject.scene.name != "PhaseUmbrella") throw new InvalidOperationException("Open PhaseUmbrella in Play mode first.");
        var result = new TaskCompletionSource<string>();
        sandbox.StartCoroutine(Check(sandbox, result));
        return result.Task;
    }

    static IEnumerator Check(PhaseSandbox sandbox, TaskCompletionSource<string> result)
    {
        var background = Application.runInBackground;
        var focus = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        Keyboard keyboard = null;
        try
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return null;
            keyboard = InputSystem.AddDevice<Keyboard>("PhaseLiveVerification");
            var c = sandbox.Controller; c.Reset();
            string directory = Path.Combine("TestResults", "Umbrella"); Directory.CreateDirectory(directory);
            int next = 0, screen = 0, frames = 0;
            while (!sandbox.Won && frames++ < 60 * 30)
            {
                var p = sandbox.Controller.player;
                if (sandbox.Controller.deaths > 0) { result.SetException(new InvalidOperationException($"Died at x={p.x:F0} in phase {sandbox.Controller.phase} after {next} jumps.")); yield break; }
                bool rising = !p.grounded && p.vy < 0;
                bool jump = next < Jumps.Length && p.grounded && p.x >= Jumps[next];
                if (jump) next++;
                // A player holds the key through the rise; a tap is a 26px hop that cannot clear a pit.
                InputSystem.QueueStateEvent(keyboard, jump || rising ? new KeyboardState(Key.RightArrow, Key.Z) : new KeyboardState(Key.RightArrow));
                if (p.x >= screen * 320 + 160 && screen < 4) File.WriteAllBytes(Path.Combine(directory, $"screen-{++screen}.png"), sandbox.Frame.EncodeToPNG());
                yield return null;
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            result.SetResult(sandbox.Won ? $"passed: {next} jumps, 0 deaths, {sandbox.Controller.time:F1}s" : $"no victory after {frames} frames at x={sandbox.Controller.player.x:F0}");
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = focus;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background;
        }
    }
}
