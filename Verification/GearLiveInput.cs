using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Plays PhaseGear with the virtual keyboard along the intended route: every floor lands blue bar, green platform, red bar or platform.
// Fails on a death or a ceiling bonk. Saves the second floor's green-to-red jump and any bonk to TestResults/Gear/.
public static class GearLiveInput
{
    // Held-key spans as frames:x:jump, one controller step per frame.
    const string Route = "14:1:1,24:1:0,14:1:1,20:1:0,14:1:1,28:1:0,14:1:1,9:1:0,12:-1:0,14:-1:1,32:-1:0,14:-1:1,20:-1:0,4:1:0,14:-1:1,9:-1:0,8:1:0,14:1:1,20:1:0,8:-1:0,14:1:1,17:1:0,8:-1:0,14:1:1,14:1:0,16:-1:0,14:-1:1,40:-1:0,14:-1:1,20:-1:0,32:1:0,14:-1:1,11:0:0,4:-1:0,14:1:1,24:1:0,14:1:1,28:1:0,6:1:1,18:1:0,14:1:1,17:1:0,14:1:1,6:1:0,120:1:0";

    public static Task<string> Run()
    {
        var sandbox = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        if (sandbox == null || !Application.isPlaying || sandbox.gameObject.scene.name != "PhaseGear" || sandbox.Won) throw new InvalidOperationException("Open PhaseGear in a fresh Play session first.");
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
        float captureDelta = Time.captureDeltaTime;
        Keyboard keyboard = null;
        try
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            PhaseSandbox.ApplyControls(PhaseSandbox.DefaultSlots);
            Time.captureDeltaTime = (float)PhaseController.Dt;
            yield return null;
            keyboard = InputSystem.AddDevice<Keyboard>("PhaseLiveVerification");
            var c = sandbox.Controller; c.Reset(); int deaths = c.deaths, bonks = 0;
            string directory = Path.Combine("TestResults", "Gear"); Directory.CreateDirectory(directory);
            int sixth = -1;
            foreach (var span in Route.Split(','))
            {
                var f = span.Split(':'); int frames = int.Parse(f[0]), x = int.Parse(f[1]); bool jump = f[2] == "1";
                for (int n = 0; n < frames; n++)
                {
                    var keys = new[] { x < 0 ? Key.LeftArrow : x > 0 ? Key.RightArrow : Key.None, jump ? Key.Z : Key.None }.Where(k => k != Key.None).ToArray();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                    double vy = sandbox.Controller.player.vy;
                    yield return null;
                    if (sandbox.Won || sandbox.Controller.ExitDirection > 0) goto done;
                    var p = sandbox.Controller.player;
                    if (sandbox.Controller.deaths > deaths) { result.SetException(new InvalidOperationException($"Died at x={p.x:F0} y={p.y:F0} after {sandbox.Controller.jumps} jumps, {bonks} bonk(s).")); yield break; }
                    if (vy < -20 && p.vy == 0 && !p.grounded && bonks++ == 0) File.WriteAllBytes(Path.Combine(directory, "bonk.png"), sandbox.Frame.EncodeToPNG());
                    if (sandbox.Controller.jumps == 6 && sixth < 0) sixth = 0;
                    if (sixth >= 0 && sixth++ == 12) File.WriteAllBytes(Path.Combine(directory, "floor2-jump.png"), sandbox.Frame.EncodeToPNG());
                }
            }
            done:
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            bool exited = sandbox.Won || sandbox.Controller.ExitDirection > 0;
            if (!exited || bonks > 0) result.SetException(new InvalidOperationException($"exited {exited}, {bonks} bonk(s) at x={sandbox.Controller.player.x:F0}"));
            else result.SetResult($"passed: {sandbox.Controller.jumps} jumps, 0 deaths, 0 bonks, {sandbox.Controller.time:F1}s");
        }
        finally
        {
            Time.captureDeltaTime = captureDelta;
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
