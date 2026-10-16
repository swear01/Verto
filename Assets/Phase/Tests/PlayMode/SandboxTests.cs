using System.Collections;
using System.IO;
using NUnit.Framework;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SandboxTests : InputTestFixture
{
    [UnityTest]
    public IEnumerator CustomControlsPauseValidationAndRoomTravel()
    {
        yield return SceneManager.LoadSceneAsync("PhaseSandbox");
        var sandbox = Object.FindAnyObjectByType<PhaseSandbox>();
        var k = InputSystem.AddDevice<Keyboard>();
        const string defaults = "{\"keys\":[\"\",\"\",\"\",\"\",\"\",\"\",\"\"]}";
        try
        {
            sandbox.ConfigureControls("{\"keys\":[\"\",\"L\",\"\",\"\",\"J\",\"K\",\"H\"]}");
            InputSystem.QueueStateEvent(k, new KeyboardState(Key.RightArrow, Key.Z, Key.X, Key.C));
            yield return null;
            var input = PhaseSandbox.ReadControls(k);
            Assert.That((input.x, input.jumpHeld, input.dashHeld, input.grab), Is.EqualTo((0, false, false, false)));
            sandbox.SetControlsOpen("true");
            double time = sandbox.Controller.time;
            InputSystem.QueueStateEvent(k, new KeyboardState(Key.J));
            yield return new WaitForSeconds(.1f);
            Assert.That(sandbox.Controller.time, Is.EqualTo(time));
            sandbox.SetControlsOpen("false");
            yield return new WaitForSeconds(.1f);
            Assert.That(sandbox.Controller.time, Is.EqualTo(time), "held capture key cannot leak into gameplay");
            InputSystem.QueueStateEvent(k, new KeyboardState()); yield return null; yield return null;
            sandbox.Controller.Reset();
            InputSystem.QueueStateEvent(k, new KeyboardState(Key.J));
            yield return new WaitForSeconds(.05f);
            Assert.That(sandbox.Controller.jumps, Is.EqualTo(1));
            InputSystem.QueueStateEvent(k, new KeyboardState(Key.K, Key.UpArrow, Key.H));
            yield return new WaitForSeconds(.1f);
            Assert.That(sandbox.Controller.dashes, Is.EqualTo(1));
            Assert.That(PhaseSandbox.ReadControls(k).grab, Is.True);
            foreach (string invalid in new[] { "{}", "{\"keys\":[null]}", "{\"keys\":[\"\",\"L\",\"\",\"\",\"L\",\"K\",\"H\"]}", "{\"keys\":[\"\",\"Escape\",\"\",\"\",\"J\",\"K\",\"H\"]}" }) sandbox.ConfigureControls(invalid);
            InputSystem.QueueStateEvent(k, new KeyboardState(Key.L)); yield return null;
            Assert.That(PhaseSandbox.ReadControls(k).x, Is.EqualTo(1), "invalid overrides are atomic");
            InputSystem.QueueStateEvent(k, new KeyboardState());
            yield return SceneManager.LoadSceneAsync("PhaseRoom2");
            InputSystem.QueueStateEvent(k, new KeyboardState(Key.L)); yield return null;
            Assert.That(PhaseSandbox.ReadControls(k).x, Is.EqualTo(1), "bindings survive room loading");
        }
        finally
        {
            var current = Object.FindAnyObjectByType<PhaseSandbox>();
            if (current != null) { current.ConfigureControls(defaults); current.SetControlsOpen("false"); }
            InputSystem.RemoveDevice(k);
        }
    }

    [UnityTest]
    public IEnumerator SceneKeyboardPixelFrameAndNoText()
    {
        yield return SceneManager.LoadSceneAsync("PhaseSandbox");
        var sandbox=Object.FindAnyObjectByType<PhaseSandbox>();Assert.That(sandbox,Is.Not.Null);
        yield return null;
        Assert.That(sandbox.Frame.width,Is.EqualTo(320));Assert.That(sandbox.Frame.height,Is.EqualTo(180));Assert.That(sandbox.Frame.filterMode,Is.EqualTo(FilterMode.Point));
        var k=InputSystem.AddDevice<Keyboard>();
        try
        {
            foreach(var move in new[] { (key:Key.W,x:0,y:-1), (key:Key.A,x:-1,y:0), (key:Key.S,x:0,y:1), (key:Key.D,x:1,y:0) })
            {
                InputSystem.QueueStateEvent(k,new KeyboardState(move.key));
                yield return null;
                var controls=PhaseSandbox.ReadControls(k);
                Assert.That((controls.x,controls.y),Is.EqualTo((move.x,move.y)),move.key.ToString());
            }
            double before=sandbox.Controller.player.x;
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.RightArrow));
            yield return new WaitForSeconds(.25f);
            Assert.That(sandbox.Controller.player.x,Is.GreaterThan(before));
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.Z));
            yield return new WaitForSeconds(.05f);
            Assert.That(sandbox.Controller.phase,Is.EqualTo(1));Assert.That(sandbox.Controller.jumps,Is.EqualTo(1));
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.X,Key.UpArrow));
            yield return new WaitForSeconds(.2f);
            Assert.That(sandbox.Controller.dashes,Is.EqualTo(1));Assert.That(sandbox.Controller.phase,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None),Is.Empty);
            Assert.That(Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None),Is.Empty);
            var r=PhaseSandbox.Viewport(1000,600);Assert.That(r.width,Is.EqualTo(960));Assert.That(r.height,Is.EqualTo(540));
            LogAssert.NoUnexpectedReceived();
            string path=System.Environment.GetEnvironmentVariable("PHASE_CAPTURE_PATH");
            if(!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path,sandbox.Frame.EncodeToPNG());
                string screen=Path.ChangeExtension(path,"game.png");
                if(File.Exists(screen))File.Delete(screen);
                // Unity's Metal screenshot backend can emit depth warnings after gameplay checks.
                ScreenCapture.CaptureScreenshot(screen);
                for(int i=0;i<60&&!File.Exists(screen);i++)yield return null;
                Assert.That(File.Exists(screen),Is.True,"actual Game View capture");
                var rendered=new Texture2D(2,2);
                try
                {
                    rendered.LoadImage(File.ReadAllBytes(screen));
                    var viewport=PhaseSandbox.Viewport(rendered.width,rendered.height);
                    int scale=(int)viewport.width/320;
                    Color32 expected=sandbox.Frame.GetPixel(0,179);
                    for(int y=0;y<scale;y++)for(int x=0;x<scale;x++)
                    {
                        Color32 actual=rendered.GetPixel((int)viewport.x+x,rendered.height-1-(int)viewport.y-y);
                        Assert.That(actual.r,Is.EqualTo(expected.r).Within(2),"display red");
                        Assert.That(actual.g,Is.EqualTo(expected.g).Within(2),"display green");
                        Assert.That(actual.b,Is.EqualTo(expected.b).Within(2),"display blue");
                    }
                }
                finally { Object.Destroy(rendered); }
            }
        }
        finally { InputSystem.RemoveDevice(k); }
    }
    [UnityTest]
    public IEnumerator ConnectedEdgesLoadBothScenesAndRespawnLocally()
    {
        yield return SceneManager.LoadSceneAsync("PhaseSandbox");
        var k=InputSystem.AddDevice<Keyboard>();
        try
        {
            var sandbox=Object.FindAnyObjectByType<PhaseSandbox>();
            sandbox.Controller.player.x=0;sandbox.Controller.player.vx=-90;
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.LeftArrow));
            yield return new WaitForSeconds(.1f);
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("PhaseSandbox"));Assert.That(sandbox.Controller.player.x,Is.Zero);
            var p=sandbox.Controller.player;p.x=311;p.y=100;p.vx=90;p.vy=-30;p.dash=0;p.stamina=63;
            sandbox.Controller.phase=2;
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.RightArrow));
            double deadline=Time.realtimeSinceStartupAsDouble+5;
            while(SceneManager.GetActiveScene().name!="PhaseRoom2"&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("PhaseRoom2"));
            InputSystem.QueueStateEvent(k,new KeyboardState());
            sandbox=Object.FindAnyObjectByType<PhaseSandbox>();
            Assert.That(sandbox.Controller.player,Is.SameAs(p));Assert.That(sandbox.Controller.phase,Is.EqualTo(2));
            Assert.That(p.x,Is.LessThan(32));Assert.That(p.y,Is.LessThan(110));Assert.That(p.dash,Is.Zero);Assert.That(p.stamina,Is.EqualTo(63));
            yield return new WaitForSeconds(.15f);
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("PhaseRoom2"));
            p.x=312;p.y=149;p.vx=90;p.vy=0;
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.RightArrow));
            yield return new WaitForSeconds(.1f);
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("PhaseRoom2"));Assert.That(p.x,Is.EqualTo(312));
            InputSystem.QueueStateEvent(k,new KeyboardState());p.x=32;p.y=200;
            yield return new WaitForSeconds(.4f);
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("PhaseRoom2"));
            Assert.That(sandbox.Controller.deaths,Is.EqualTo(1));Assert.That(sandbox.Controller.player.x,Is.EqualTo(sandbox.spawn.x));
            p=sandbox.Controller.player;p.x=1;p.y=100;p.vx=-90;p.vy=-30;p.dash=0;
            sandbox.Controller.phase=1;
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.LeftArrow));
            deadline=Time.realtimeSinceStartupAsDouble+5;
            while(SceneManager.GetActiveScene().name!="PhaseSandbox"&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("PhaseSandbox"));
            InputSystem.QueueStateEvent(k,new KeyboardState());
            sandbox=Object.FindAnyObjectByType<PhaseSandbox>();
            Assert.That(sandbox.Controller.player,Is.SameAs(p));Assert.That(p.x,Is.GreaterThan(280));
            Assert.That(sandbox.Controller.phase,Is.EqualTo(1));Assert.That(p.dash,Is.Zero);Assert.That(sandbox.Controller.deaths,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<PhaseSandbox>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }
        finally { InputSystem.RemoveDevice(k); }
    }

}
