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
    [UnityTest]
    public IEnumerator ImportRejectsUnsafeMapsAtomicallyAndValidMapsRemainPlayable()
    {
        yield return SceneManager.LoadSceneAsync("PhaseDemo1");
        var original=Object.FindAnyObjectByType<PhaseSandbox>();var controller=original.Controller;
        original.SetControlsOpen("true");
        foreach(string value in new[]{null,"","02","9","PhaseDemo2"}) original.SelectDemoRoom(value);
        foreach(string json in new[]{null,"{}",new string(' ',PhaseCustomMap.MaxJsonLength+1),
            "{\"blocks\":[{\"x\":0,\"y\":168,\"w\":320,\"h\":8,\"phase\":-1,\"phaseMask\":7}],\"spawnX\":1,\"spawnY\":152}"})
        {
            original.LoadCustomMap(json);Assert.That(original.gameObject.activeSelf,Is.True);
            Assert.That(original.Controller,Is.SameAs(controller));
        }
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("PhaseDemo1"));
        var map=new PhaseDemoRoom{name="Custom",spawnX=24,spawnY=152,
            blocks=new[]{new Block(0,168,320,8,phaseMask:7)},
            spikes=new[]{new DemoSpike{x=80,y=160,length=8,direction=SpikeDirection.Up,phaseMask=7}}};
        original.LoadCustomMap(JsonUtility.ToJson(map));yield return null;
        var sandbox=Object.FindAnyObjectByType<PhaseSandbox>();
        Assert.That(original==null,Is.True);Assert.That(sandbox.gameObject.name,Is.EqualTo("PHASE"));
        Assert.That(Object.FindObjectsByType<PhaseSandbox>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
        Assert.That(sandbox.GetComponent<Grid>().cellSize,Is.EqualTo(new Vector3(8,8,1)));
        Assert.That(sandbox.Frame.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(sandbox.Controller.blocks.Count,Is.EqualTo(1));Assert.That(sandbox.Controller.spikes.Count,Is.EqualTo(1));
        Assert.That(sandbox.Controller.exitLeft||sandbox.Controller.exitRight,Is.False);
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
            sandbox.SelectDemoRoom("2");double deadline=Time.realtimeSinceStartupAsDouble+5;
            while(SceneManager.GetActiveScene().name!="PhaseDemo2"&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("PhaseDemo2"));
            yield return null;var next=Object.FindAnyObjectByType<PhaseSandbox>();
            Assert.That(next.Controller.phase,Is.Zero);Assert.That(next.Frame,Is.Not.Null);
            Assert.That(Object.FindObjectsByType<PhaseSandbox>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            var current=Object.FindAnyObjectByType<PhaseSandbox>();if(current!=null)current.SetControlsOpen("false");
        }
    }
}
