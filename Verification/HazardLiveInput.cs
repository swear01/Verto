using System;
using System.Collections;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class HazardLiveInput
{
    public static Task<string> Run()
    {
        var sandbox = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        if (sandbox == null || !Application.isPlaying) throw new InvalidOperationException("Open the HazardScene probe in Play mode first.");
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
            var s=sandbox.Controller;s.Reset();int deaths=s.deaths;
            if (!Require(s.spikes.Count==4 && s.blocks.Exists(b=>b.Mask==3) && s.blocks.Exists(b=>b.Mask==5) && s.blocks.Exists(b=>b.Mask==6), "scene collects authored masks and spike components",result)) yield break;
            s.player.x=120;s.player.y=96;s.player.grounded=false;s.player.coyote=.1;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Z));yield return new WaitForSeconds(.05f);
            if (!Require(s.phase==1 && s.deaths==deaths+1 && s.respawn>0,"jump activates blue spikes and kills immediately",result)) yield break;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.35f);
            if (!Require(s.phase==0 && s.player.x==32 && s.respawn==0,"respawn restores phase and position",result)) yield break;
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
