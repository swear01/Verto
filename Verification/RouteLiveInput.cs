using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Plays the open level's recorded route (Verification/Routes/<scene>.json) with the virtual keyboard, following the
// controller's own frame count, and requires no deaths and an exit at the right edge. Saves frames to TestResults/Route/.
public static class RouteLiveInput
{
    public static Task<string> Run()
    {
        var sandbox = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        if (sandbox == null || !Application.isPlaying || sandbox.Won) throw new InvalidOperationException("Open a level in a fresh Play session first.");
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
        string scene = sandbox.gameObject.scene.name;
        var inputs = PhaseRoute.Inputs(PhaseRoute.Parse(File.ReadAllText(PhaseRoute.RoutePath(scene)))).ToArray();
        Keyboard keyboard = null;
        try
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            PhaseSandbox.ApplyControls(PhaseSandbox.DefaultSlots);
            yield return null;
            keyboard = InputSystem.AddDevice<Keyboard>("PhaseLiveVerification");
            var c = sandbox.Controller; c.Reset(); int deaths = c.deaths; double start = c.time;
            string directory = Path.Combine("TestResults", "Route"); Directory.CreateDirectory(directory);
            int shot = 0, frame = 0;
            while (!sandbox.InTransition && c.ExitDirection == 0 && frame < inputs.Length + 120)
            {
                if (c.deaths > deaths) { result.SetException(new InvalidOperationException($"{scene}: died at x={c.player.x:F0} y={c.player.y:F0} on route frame {frame}.")); yield break; }
                frame = (int)Math.Round((c.time - start) / PhaseController.Dt);
                var input = frame < inputs.Length ? inputs[frame] : new Controls { x = 1 };
                var keys = new System.Collections.Generic.List<Key>();
                if (input.x < 0) keys.Add(Key.LeftArrow);
                if (input.x > 0) keys.Add(Key.RightArrow);
                if (input.jumpHeld) keys.Add(Key.Z);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
                if (frame >= shot * 90 && shot < 8) File.WriteAllBytes(Path.Combine(directory, $"{scene}-{shot++}.png"), sandbox.Frame.EncodeToPNG());
                yield return null;
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            bool exited = sandbox.InTransition || c.ExitDirection > 0;
            if (exited && c.deaths == deaths) result.SetResult($"passed: {scene}, {c.jumps} jumps, 0 deaths, {c.time - start:F1}s");
            else result.SetException(new InvalidOperationException($"{scene}: no exit after the route, x={c.player.x:F0} y={c.player.y:F0}."));
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            PhaseSandbox.ApplyControls(slots);
            InputSystem.settings.backgroundBehavior = focus;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background;
            result.TrySetException(new InvalidOperationException("The route stopped early; see the Console."));
        }
    }
}
