using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Plays PhaseUmbrella (Run) or the chaos level (Chaos) with the virtual keyboard: hold right and jump at the intended spots,
// without dying, out the right edge. Saves a frame per screen to TestResults/<level>/.
public static class UmbrellaLiveInput
{
    static readonly double[] Jumps = { 40, 100, 256, 352, 432, 496, 592, 656, 700, 768, 928, 1000, 1040, 1104, 1168 };
    // Verification/ChaosLevel.py's route.
    static readonly double[] ChaosJumps = { 40, 232, 328, 528, 588, 672, 736, 848, 990, 1168 };

    public static Task<string> Run() => Play("PhaseUmbrella", Jumps, "Umbrella");
    public static Task<string> Chaos() => Play("PhaseDemo15", ChaosJumps, "Chaos");

    static Task<string> Play(string scene, double[] jumps, string folder)
    {
        var sandbox = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        if (sandbox == null || !Application.isPlaying || sandbox.gameObject.scene.name != scene || sandbox.Won) throw new InvalidOperationException($"Open {scene} in a fresh Play session first.");
        var result = new TaskCompletionSource<string>();
        sandbox.StartCoroutine(Check(sandbox, jumps, folder, result));
        return result.Task;
    }

    static IEnumerator Check(PhaseSandbox sandbox, double[] jumps, string folder, TaskCompletionSource<string> result)
    {
        var background = Application.runInBackground;
        var focus = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var slots = PhaseSandbox.Slots.ToArray();
        Keyboard keyboard = null;
        try
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            PhaseSandbox.ApplyControls(PhaseSandbox.DefaultSlots);
            yield return null;
            keyboard = InputSystem.AddDevice<Keyboard>("PhaseLiveVerification");
            var c = sandbox.Controller; c.Reset(); int deaths = c.deaths;
            string directory = Path.Combine("TestResults", folder); Directory.CreateDirectory(directory);
            int next = 0, screen = 0;
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!sandbox.Won && sandbox.Controller.ExitDirection == 0 && Time.realtimeSinceStartupAsDouble < deadline)
            {
                var p = sandbox.Controller.player;
                if (sandbox.Controller.deaths > deaths) { result.SetException(new InvalidOperationException($"Died at x={p.x:F0} in phase {sandbox.Controller.phase} after {next} jumps.")); yield break; }
                bool rising = !p.grounded && p.vy < 0;
                bool jump = next < jumps.Length && p.grounded && p.x >= jumps[next];
                if (jump) next++;
                // A player holds the key through the rise; a tap is a 26px hop that cannot clear a pit.
                InputSystem.QueueStateEvent(keyboard, jump || rising ? new KeyboardState(Key.RightArrow, Key.Z) : new KeyboardState(Key.RightArrow));
                if (p.x >= screen * 320 + 160 && screen < 4) File.WriteAllBytes(Path.Combine(directory, $"screen-{++screen}.png"), sandbox.Frame.EncodeToPNG());
                yield return null;
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            result.SetResult(sandbox.Won || sandbox.Controller.ExitDirection > 0 ? $"passed: {next} jumps, 0 deaths, {sandbox.Controller.time:F1}s" : $"no exit after 30s at x={sandbox.Controller.player.x:F0}");
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            PhaseSandbox.ApplyControls(slots);
            InputSystem.settings.backgroundBehavior = focus;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background;
            // An exception in the route reaches the Console; this only stops the caller from waiting forever.
            result.TrySetException(new InvalidOperationException("The route stopped early; see the Console."));
        }
    }
}
