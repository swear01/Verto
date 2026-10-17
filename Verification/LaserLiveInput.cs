using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class LaserLiveInput
{
    public static Task<string> Run()
    {
        var sandbox=UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        if(!Application.isPlaying || sandbox==null || sandbox.gameObject.scene.name!="PhaseDemo3")
            throw new InvalidOperationException("Open PhaseDemo3 in Play first.");
        var result=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);sandbox.StartCoroutine(Check(sandbox,result));return result.Task;
    }
    static IEnumerator Check(PhaseSandbox sandbox,TaskCompletionSource<string> result)
    {
        bool background=Application.runInBackground;float captureDelta=Time.captureDeltaTime;
        var focus=InputSystem.settings.backgroundBehavior;var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        var s=sandbox.Controller;var authored=s.lasers.ToArray();Keyboard keyboard=null;
        try
        {
            Application.runInBackground=true;Time.captureDeltaTime=(float)PhaseController.Dt;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return null;keyboard=InputSystem.AddDevice<Keyboard>("PhaseLaserVerification");
            s.Reset();int deaths=s.deaths,frame=Time.frameCount;double time=s.time;
            if(!Require(authored.Length==1 && authored[0].Mask==7,"scene collects the authored white emitter",result))yield break;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));
            for(int i=0;i<25;i++)yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow,Key.UpArrow,Key.C));
            for(int i=0;i<95;i++)yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            if(!Require(s.gears[0].Lift==24 && s.LaserBeam(authored[0]).y==s.gears[0].platform.y+s.gears[0].platform.h && s.deaths==deaths,
                "keyboard climbing moves the shield across the white beam",result))yield break;
            s.player.x=authored[0].x;s.player.y=s.gears[0].platform.y-s.player.h;s.player.vx=s.player.vy=0;
            for(int i=0;i<3;i++)yield return null;
            if(!Require(s.deaths==deaths,"white platform protects the stationary rider",result))yield break;
            Directory.CreateDirectory("TestResults/Lasers");
            File.WriteAllBytes("TestResults/Lasers/white-shield.png",sandbox.Frame.EncodeToPNG());
            s.gears[0].platform.x=88;yield return null;yield return null;
            if(!Require(s.deaths==deaths+1,"departing cover immediately exposes a stationary player",result))yield break;
            s.Reset();s.lasers.Clear();s.lasers.Add(new Laser(136,168,LaserDirection.Up,2));
            s.player.x=140;s.player.y=100;s.player.grounded=true;s.player.coyote=.1;
            yield return null;
            File.WriteAllBytes("TestResults/Lasers/safe-blue.png",sandbox.Frame.EncodeToPNG());
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Z));
            for(int i=0;i<3;i++)yield return null;
            if(!Require(s.phase==1 && s.deaths==deaths+2 && s.respawn>0,
                $"keyboard jump activates blue: phase={s.phase}, deaths={s.deaths-deaths}, respawn={s.respawn}",result))yield break;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            for(int i=0;i<20;i++)yield return null;
            if(!Require(s.phase==0 && s.respawn==0 && s.player.dash==1 && s.player.stamina==110,"respawn restores the checkpoint",result))yield break;
            if(!Require(Time.frameCount>frame && s.time>time,"frames and controller time advance",result))yield break;
            result.SetResult($"passed: frames={Time.frameCount-frame}, seconds={s.time-time:F3}, white shield, exposure, blue activation and respawn");
        }
        finally
        {
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            s.lasers.Clear();s.lasers.AddRange(authored);s.Reset();
            InputSystem.settings.backgroundBehavior=focus;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            Time.captureDeltaTime=captureDelta;Application.runInBackground=background;
        }
    }
    static bool Require(bool condition,string message,TaskCompletionSource<string> result)
    {if(condition)return true;result.TrySetException(new InvalidOperationException(message));return false;}
}
