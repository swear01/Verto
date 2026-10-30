using System.Collections;
using NUnit.Framework;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class MapBridgeTests : InputTestFixture
{
    [System.Serializable] sealed class MapData
    {
        public string name;
        public Block[] blocks;
        public DemoSpike[] spikes;
        public double spawnX,spawnY;
    }
    [UnityTest]
    public IEnumerator ImportRejectsUnsafeMapsAtomicallyAndValidMapsRemainPlayable()
    {
        yield return SceneManager.LoadSceneAsync("PhaseDemo1");
        var original=Object.FindAnyObjectByType<PhaseSandbox>();var controller=original.Controller;
        original.SetControlsOpen("true");
        foreach(string value in new[]{null,"","02","09",(PhaseSandbox.Levels.Length+1).ToString(),"10 ","+10","PhaseDemo13"}) original.SelectDemoRoom(value);
        foreach(string json in new[]{null,"{}",new string(' ',PhaseCustomMap.MaxJsonLength+1),
            "{\"blocks\":[{\"x\":0,\"y\":168,\"w\":320,\"h\":8,\"phase\":-1,\"phaseMask\":7}],\"spawnX\":1,\"spawnY\":152}",
            "{\"blocks\":[{\"x\":0,\"y\":168,\"w\":320,\"h\":8,\"phase\":-1,\"phaseMask\":7}],\"spawnX\":24,\"spawnY\":152,\"movers\":[{}]}",
            "{\"blocks\":[{\"x\":0,\"y\":168,\"w\":320,\"h\":8,\"phase\":-1,\"phaseMask\":7}],\"spawnX\":24,\"spawnY\":152,\"gears\":[{}]}",
            "{\"blocks\":[{\"x\":0,\"y\":168,\"w\":320,\"h\":8,\"phase\":-1,\"phaseMask\":7}],\"spawnX\":24,\"spawnY\":152,\"lasers\":[{\"x\":24,\"y\":0,\"direction\":1,\"phaseMask\":1}]}"})
        {
            original.LoadCustomMap(json);Assert.That(PhaseSandbox.LastMapError,Is.Not.Empty);
            Assert.That(original.gameObject.activeSelf,Is.True);
            Assert.That(original.Controller,Is.SameAs(controller));
        }
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("PhaseDemo1"));
        var map=new PhaseDemoRoom{name="Custom",spawnX=24,spawnY=152,
            blocks=new[]{new Block(0,168,320,8,phaseMask:7)},
            spikes=new[]{new DemoSpike{x=80,y=160,length=8,direction=SpikeDirection.Up,phaseMask=7}}};
        original.LoadCustomMap(JsonUtility.ToJson(new MapData { name=map.name,blocks=map.blocks,spikes=map.spikes,spawnX=map.spawnX,spawnY=map.spawnY }));
        Assert.That(PhaseSandbox.LastMapError,Is.Empty,PhaseSandbox.LastMapError);yield return null;
        var sandbox=Object.FindAnyObjectByType<PhaseSandbox>();
        Assert.That(original==null,Is.True);Assert.That(sandbox.gameObject.name,Is.EqualTo("PHASE"));
        Assert.That(Object.FindObjectsByType<PhaseSandbox>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
        Assert.That(sandbox.Frame.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(sandbox.Controller.blocks.Count,Is.EqualTo(40),"a version 1 floor imports as one block per cell");Assert.That(sandbox.Controller.spikes.Count,Is.EqualTo(1));
        Assert.That(sandbox.Controller.exitLeft||sandbox.Controller.exitRight,Is.False);
        var previous = sandbox.gameObject;
        map.lasers=new[]{new DemoLaser{x=160,y=160,direction=LaserDirection.Up,phaseMask=7}};
        map.crystals=new[]{new DemoCrystal{x=80,y=80,phaseMask=2}};
        sandbox.LoadCustomMap(PhaseCustomMap.ToJson(new PhaseWorld { rooms = new[] { map } }));
        Assert.That(PhaseSandbox.LastMapError, Is.Empty, PhaseSandbox.LastMapError); yield return null;
        sandbox = Object.FindAnyObjectByType<PhaseSandbox>();
        Assert.That(previous == null, Is.True, "reimport disposes the previous map");
        Assert.That(sandbox.Controller.blocks.Count, Is.EqualTo(40));
        Assert.That((sandbox.Controller.lasers.Count, sandbox.Controller.crystals.Count), Is.EqualTo((1, 1)));
        var keyboard=InputSystem.AddDevice<Keyboard>();
        try
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
            double start=sandbox.Controller.player.x,time=sandbox.Controller.time;int frame=Time.frameCount;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));yield return new WaitForSeconds(.2f);
            Assert.That(Time.frameCount,Is.GreaterThan(frame));Assert.That(sandbox.Controller.time,Is.GreaterThan(time));
            Assert.That(sandbox.Controller.player.x,Is.GreaterThan(start));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.1f);
            sandbox.Controller.player.x=24;sandbox.Controller.player.vx=0;sandbox.Controller.player.y=200;
            yield return new WaitForSeconds(.4f);
            Assert.That(sandbox.Controller.deaths,Is.EqualTo(1));Assert.That(sandbox.Controller.player.x,Is.EqualTo(24));
            sandbox.SelectDemoRoom("3");double deadline=Time.realtimeSinceStartupAsDouble+5;
            while(SceneManager.GetActiveScene().name!="PhaseDemo13"&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("PhaseDemo13"));
            yield return null;var next=Object.FindAnyObjectByType<PhaseSandbox>();
            Assert.That(next.Controller.phase,Is.Zero);Assert.That(next.Frame,Is.Not.Null);
            Assert.That(Object.FindObjectsByType<PhaseSandbox>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            foreach (var room in new[] { ("4", "PhaseDemo14", 0), ("7", "PhaseGear", 0) })
            {
                next.SelectDemoRoom(room.Item1); deadline = Time.realtimeSinceStartupAsDouble + 5;
                while (SceneManager.GetActiveScene().name != room.Item2 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(room.Item2)); yield return null;
                next = Object.FindAnyObjectByType<PhaseSandbox>();
                Assert.That(next.Controller.phase, Is.Zero);
                Assert.That(next.Controller.crystals.Count, Is.EqualTo(room.Item3));
                Assert.That(next.GetComponent<PhaseGrid>(), Is.Not.Null);
                Assert.That(Object.FindObjectsByType<PhaseSandbox>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            }
            Assert.That(next.Won, Is.False, "selecting a level is not a victory");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Z)); yield return new WaitForSeconds(.08f);
            Assert.That(next.Controller.phase, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            var current=Object.FindAnyObjectByType<PhaseSandbox>();if(current!=null)current.SetControlsOpen("false");
        }
    }
}
