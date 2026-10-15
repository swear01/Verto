using System;
using System.Collections;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class LiveInput
{
    public static Task<string> Run()
    {
        var sandbox = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        if (sandbox == null || !Application.isPlaying) throw new InvalidOperationException("Open PhaseSandbox in Play mode first.");
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
            // Queue on the player loop so Editor input processing cannot consume the key edge.
            yield return null;
            keyboard = InputSystem.AddDevice<Keyboard>("PhaseLiveVerification");
            sandbox.Controller.Reset();
            double start = sandbox.Controller.player.x;
            int jumps = sandbox.Controller.jumps, dashes = sandbox.Controller.dashes;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            yield return new WaitForSeconds(.15f);
            if (!Require(sandbox.Controller.player.x > start, "right arrow moves the player", result)) yield break;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Z));
            yield return new WaitForSeconds(.05f);
            if (!Require(sandbox.Controller.phase == 1 && sandbox.Controller.jumps == jumps + 1, "jump changes phase once", result)) yield break;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.X, Key.UpArrow));
            yield return new WaitForSeconds(.2f);
            if (!Require(sandbox.Controller.dashes == dashes + 1 && sandbox.Controller.phase == 1, "dash keeps phase", result)) yield break;
            result.SetResult("passed");
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = focus;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background;
        }
    }

    static bool Require(bool condition, string message, TaskCompletionSource<string> result)
    {
        if (condition) return true;
        result.SetException(new InvalidOperationException(message));
        return false;
    }
}
