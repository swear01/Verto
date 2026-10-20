using System;
using System.IO;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class ColorPlatformLiveInput
{
    // White floor, a blue returning platform the player can walk into while red, and a green gear.
    const string Map = @"{""name"":""Colored platforms"",""blocks"":[{""x"":0,""y"":168,""w"":320,""h"":8,""phaseMask"":7}],""spikes"":[],
""platforms"":[{""w"":24,""h"":8,""nodeX"":[48,48],""nodeY"":[152,120],""returns"":true,""phaseMask"":2}],
""gears"":[{""wall"":{""x"":200,""y"":112,""w"":8,""h"":56},""platform"":{""x"":216,""y"":104,""w"":56,""h"":8},""top"":88,""endX"":240,""phaseMask"":4}],
""spawnX"":16,""spawnY"":152}";
    static async Task Frames(int count)
    {
        int target=Time.frameCount+count;double deadline=Time.realtimeSinceStartupAsDouble+30;
        while(Time.frameCount<target&&Time.realtimeSinceStartupAsDouble<deadline)await Task.Delay(16);
        if(Time.frameCount<target)throw new TimeoutException("Player loop did not advance.");
    }
    static async Task Until(Func<bool> done,int limit,string message)
    {
        for(int i=0;i<limit&&!done();i++)await Frames(1);
        if(!done())throw new InvalidOperationException(message);
    }
    static void Require(bool value,string message)
    { if(!value)throw new InvalidOperationException(message); }
    static void Capture(PhaseSandbox sandbox,string name)
    { File.WriteAllBytes("TestResults/ColorPlatforms/"+name+".png",sandbox.Frame.EncodeToPNG()); }
    public static async Task<string> Run()
    {
        var original=UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        Require(Application.isPlaying&&original!=null,"Open a level in Play mode first.");
        bool background=Application.runInBackground;
        var focus=InputSystem.settings.backgroundBehavior;
        var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        float captureDelta=Time.captureDeltaTime;Keyboard keyboard=null;
        try
        {
            Application.runInBackground=true;Time.captureDeltaTime=(float)PhaseController.Dt;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>("PhaseColorPlatformVerification");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Frames(2);
            original.LoadCustomMap(Map);Require(PhaseSandbox.LastMapError=="",PhaseSandbox.LastMapError);await Frames(2);
            var sandbox=UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();var s=sandbox.Controller;var p=s.player;var blue=s.movers[0];
            int frame=Time.frameCount;double time=s.time;
            Directory.CreateDirectory("TestResults/ColorPlatforms");
            await Until(()=>p.grounded,60,"Player did not land on the floor.");Capture(sandbox,"red-start");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));
            await Until(()=>p.x>=40,60,"Player could not walk into the inactive blue platform.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Frames(20);
            Require(PhaseController.Overlap(p,blue)&&s.phase==0&&p.y+p.h==168,"Inactive blue platform blocked the player.");
            Capture(sandbox,"inside-ghost");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Z));await Frames(1);
            Require(s.phase==1&&s.blockedParts.Contains(blue),"Blue platform did not defer while the player occupied it.");
            Capture(sandbox,"deferred");
            await Until(()=>p.grounded&&p.y+p.h==blue.y,90,"Player did not land on the solidified blue platform.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Frames(10);
            Require(s.blockedParts.Count==0&&p.y+p.h==152&&s.deaths==0,"Player is not standing on the blue platform.");
            Capture(sandbox,"blue-landed");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Z));await Frames(1);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            Require(s.phase==2,"Second jump did not switch to green.");Capture(sandbox,"green-gear");
            await Until(()=>p.grounded&&p.y+p.h==168,120,"Player did not fall through the inactive blue platform.");
            Require(s.deaths==0&&s.phase==2,"Fell-through state is wrong.");
            Capture(sandbox,"fell-through");
            int frames=Time.frameCount-frame;double seconds=s.time-time;
            Require(frames>=100&&seconds>1,"Live evidence requires advancing frames and controller time.");
            string report=$"passed: walk into inactive blue; jump defers then lands on blue; jump to green falls through; frames={frames}, controllerSeconds={seconds:F3}";
            File.WriteAllText("TestResults/ColorPlatforms/live-input.txt",report);return report;
        }
        finally
        {
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior=focus;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            Time.captureDeltaTime=captureDelta;Application.runInBackground=background;
        }
    }
}
