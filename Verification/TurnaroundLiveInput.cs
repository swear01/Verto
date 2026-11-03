using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Plays PhaseTurnaround with the virtual keyboard like a player: run right, jump at the end of the floor, then on every
// landing turn back and jump again, holding jump through the rise, out of the top shelf's right edge without dying.
// Saves a frame per screen height to TestResults/Turnaround/.
public static class TurnaroundLiveInput
{
    const double FirstJump = 116;   // Verification/Routes/PhaseTurnaround.json
    const int Jumps = 13;

    public static Task<string> Run()
    {
        var sandbox = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        if (sandbox == null || !Application.isPlaying || sandbox.gameObject.scene.name != "PhaseTurnaround" || sandbox.Won) throw new InvalidOperationException("Open PhaseTurnaround in a fresh Play session first.");
        var result = new TaskCompletionSource<string>();
        sandbox.StartCoroutine(Check(sandbox, result));
        return result.Task;
    }

    static IEnumerator Check(PhaseSandbox sandbox, TaskCompletionSource<string> result)
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
            string directory = Path.Combine("TestResults", "Turnaround"); Directory.CreateDirectory(directory);
            int jumps = 0, dir = 1, screen = 0; bool air = false, pending = false; var landings = new System.Text.StringBuilder();
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!sandbox.Won && sandbox.Controller.ExitDirection == 0 && Time.realtimeSinceStartupAsDouble < deadline)
            {
                var p = sandbox.Controller.player;
                if (sandbox.Controller.deaths > deaths) { result.SetException(new InvalidOperationException($"Died at ({p.x:F0},{p.y:F0}) in phase {sandbox.Controller.phase} after {jumps} jumps; landings{landings}.")); yield break; }
                bool jump = false;
                if (p.grounded && jumps == 0 && p.x >= FirstJump) jump = true;
                else if (p.grounded && air && jumps > 0 && jumps < Jumps) { dir = -dir; jump = true; landings.Append($" {p.x:F0},{p.y:F0}"); }
                else if (p.grounded && jumps == Jumps) dir = 1;
                if (jump) { jumps++; pending = true; }
                // Keep the key down until the jump has fired: a frame can pass without a controller step.
                if (!p.grounded) pending = false;
                air = !p.grounded;
                bool rising = !p.grounded && p.vy < 0;
                var move = dir > 0 ? Key.RightArrow : Key.LeftArrow;
                InputSystem.QueueStateEvent(keyboard, pending || rising ? new KeyboardState(move, Key.Z) : new KeyboardState(move));
                if (p.y <= 352 - 176 * screen - 88 && screen < 2) File.WriteAllBytes(Path.Combine(directory, $"screen-{++screen}.png"), sandbox.Frame.EncodeToPNG());
                yield return null;
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            result.SetResult(sandbox.Won || sandbox.Controller.ExitDirection > 0 ? $"passed: {jumps} jumps, 0 deaths, {sandbox.Controller.time:F1}s" : $"no exit after 30s at ({sandbox.Controller.player.x:F0},{sandbox.Controller.player.y:F0})");
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
