using System;
using System.IO;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public static class MapLiveInput
{
    static async Task Frames(int count)
    {
        int target=Time.frameCount+count;double deadline=Time.realtimeSinceStartupAsDouble+30;
        while(Time.frameCount<target&&Time.realtimeSinceStartupAsDouble<deadline)await Task.Delay(16);
        if(Time.frameCount<target)throw new TimeoutException("Player loop did not advance.");
    }
    static void Require(bool value,string message)
    { if(!value)throw new InvalidOperationException(message); }
    static void Capture(PhaseSandbox sandbox,string name)
    { File.WriteAllBytes("TestResults/CustomMap/"+name+".png",sandbox.Frame.EncodeToPNG()); }
    public static async Task<string> Run()
    {
        var original=UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        Require(Application.isPlaying&&original!=null,"Open a demo in Play mode first.");
        bool background=Application.runInBackground;
        var focus=InputSystem.settings.backgroundBehavior;
        var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        float captureDelta=Time.captureDeltaTime;Keyboard keyboard=null;
        try
        {
            Application.runInBackground=true;Time.captureDeltaTime=(float)PhaseController.Dt;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>("PhaseMapVerification");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Frames(2);
            original.SetControlsOpen("true");var old=original.Controller;
            original.LoadCustomMap("{}");Require(original.Controller==old&&original.gameObject.activeSelf,"Invalid input changed the current map.");
            var map=new PhaseDemoRoom{name="Verification",spawnX=24,spawnY=152,
                blocks=new[]{new Block(0,168,320,8,phaseMask:7)},
                spikes=new[]{new DemoSpike{x=96,y=160,length=8,direction=SpikeDirection.Up,phaseMask=7}}};
            original.LoadCustomMap(JsonUtility.ToJson(map));await Frames(2);
            var sandbox=UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
            Require(original==null&&sandbox!=null&&sandbox.Controller!=old,"Map root was not replaced.");
            Require(UnityEngine.Object.FindObjectsByType<PhaseSandbox>(FindObjectsSortMode.None).Length==1,"Map import left multiple players.");
            Require(sandbox.GetComponent<Grid>().cellSize==new Vector3(8,8,1),"Map grid is not 8px.");
            var s=sandbox.Controller;int frame=Time.frameCount;double time=s.time;
            Directory.CreateDirectory("TestResults/CustomMap");await Frames(30);Capture(sandbox,"start");
            double start=s.player.x;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));await Frames(10);
            Require(s.player.x>start,"Keyboard movement did not advance the imported player.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow,Key.Z));await Frames(8);
            Require(s.jumps==1&&s.phase==1,"Imported map jump did not switch phase once.");Capture(sandbox,"jump");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Frames(50);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.X,Key.UpArrow));await Frames(1);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Frames(60);
            Require(s.dashes==1&&s.phase==1&&s.deaths==0,"Imported map dash changed phase or failed.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));
            for(int i=0;i<100&&s.deaths==0;i++)await Frames(1);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            Require(s.deaths==1&&s.respawn>0,"Keyboard route did not hit the authored spikes.");Capture(sandbox,"death");
            for(int i=0;i<60&&s.respawn>0;i++)await Frames(1);
            Require(s.respawn==0&&s.player.x==map.spawnX&&s.phase==0,"Imported map did not respawn at its checkpoint.");Capture(sandbox,"respawn");
            int frames=Time.frameCount-frame;double seconds=s.time-time;
            Require(frames>=100&&seconds>1,"Live evidence requires advancing frames and controller time.");
            sandbox.SelectDemoRoom("2");double deadline=Time.realtimeSinceStartupAsDouble+15;
            while(SceneManager.GetActiveScene().name!="PhaseDemo2"&&Time.realtimeSinceStartupAsDouble<deadline)await Task.Delay(16);
            await Frames(2);var next=UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
            Require(SceneManager.GetActiveScene().name=="PhaseDemo2"&&next!=null&&next.Frame!=null,"Built-in scene selection failed.");Capture(next,"selected-room");
            string report=$"passed: invalid import atomic; custom grid map; keyboard move/jump/dash/spike death/respawn; room2 selection; frames={frames}, controllerSeconds={seconds:F3}";
            File.WriteAllText("TestResults/CustomMap/live-input.txt",report);return report;
        }
        finally
        {
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            var current=UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();if(current!=null)current.SetControlsOpen("false");
            InputSystem.settings.backgroundBehavior=focus;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            Time.captureDeltaTime=captureDelta;Application.runInBackground=background;
        }
    }
}
