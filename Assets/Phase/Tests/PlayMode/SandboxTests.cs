using System.Collections;
using System.IO;
using System.Linq;
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
        var defaults = new[] { "LeftArrow", "A", "RightArrow", "D", "UpArrow", "W", "DownArrow", "S", "Z", "Space", "X", "", "C", "Shift" };
        var custom = new[] { "LeftArrow", "A", "L", "D", "UpArrow", "W", "DownArrow", "S", "J", "Space", "K", "", "H", "Shift" };
        try
        {
            sandbox.ConfigureControls(Map(custom));
            InputSystem.QueueStateEvent(k, new KeyboardState(Key.RightArrow, Key.Z, Key.X, Key.C));
            yield return null;
            var input = PhaseSandbox.ReadControls(k);
            Assert.That((input.x, input.jumpHeld, input.dashHeld, input.grab), Is.EqualTo((0, false, false, false)));
            sandbox.SetControlsOpen("true");
            InputSystem.QueueStateEvent(k, new KeyboardState(Key.D, Key.Space)); yield return null;
            input = PhaseSandbox.ReadControls(k);
            Assert.That((input.x, input.jumpHeld), Is.EqualTo((1, true)), "secondary slots retain their defaults");
            foreach (var shift in new[] { Key.LeftShift, Key.RightShift })
            {
                InputSystem.QueueStateEvent(k, new KeyboardState(shift)); yield return null;
                Assert.That(PhaseSandbox.ReadControls(k).grab, Is.True, shift.ToString());
            }
            custom[3] = "P";
            sandbox.ConfigureControls(Map(custom));
            foreach (var right in new[] { Key.L, Key.P })
            {
                InputSystem.QueueStateEvent(k, new KeyboardState(right)); yield return null;
                Assert.That(PhaseSandbox.ReadControls(k).x, Is.EqualTo(1), "both changed slots work");
            }
            InputSystem.QueueStateEvent(k, new KeyboardState(Key.D)); yield return null;
            Assert.That(PhaseSandbox.ReadControls(k).x, Is.Zero, "only replaced secondary key stops working");
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
            foreach (string invalid in new[] { "{}", "{\"keys\":[null]}", Map(custom).Replace("\"L\"", "null") }) sandbox.ConfigureControls(invalid);
            int invalidCase = 0;
            foreach (var invalid in new System.Action<string[]>[] {
                a => a[8] = "L", a => a[2] = "Escape", a => a[2] = a[3] = "",
                a => a[3] = "L", a => a[11] = "LeftShift", a => a[2] = "Delete", a => a[2] = "1"
            })
            {
                var candidate = (string[])custom.Clone(); invalid(candidate); sandbox.ConfigureControls(Map(candidate));
                InputSystem.QueueStateEvent(k, new KeyboardState(Key.L)); yield return null;
                Assert.That(PhaseSandbox.ReadControls(k).x, Is.EqualTo(1), "invalid case " + invalidCase++);
            }
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
            if (current != null) { current.ConfigureControls(Map(defaults)); current.SetControlsOpen("false"); }
            InputSystem.RemoveDevice(k);
        }
    }

    [System.Serializable] class Bindings { public string[] keys; }
    static string Map(string[] keys) => JsonUtility.ToJson(new Bindings { keys = keys });

    [UnityTest]
    public IEnumerator SceneKeyboardPixelFrameAndNoText()
    {
        yield return SceneManager.LoadSceneAsync("PhaseSandbox");
        var sandbox=Object.FindAnyObjectByType<PhaseSandbox>();Assert.That(sandbox,Is.Not.Null);
        yield return null;
        var view=PhaseSandbox.ViewSize(Screen.width,Screen.height);
        Assert.That(sandbox.Frame.width,Is.EqualTo(view.x));Assert.That(sandbox.Frame.height,Is.EqualTo(view.y));Assert.That(sandbox.Frame.filterMode,Is.EqualTo(FilterMode.Point));
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
            yield return new WaitForFixedUpdate(); CheckNativeShapes(sandbox);
            sandbox.Controller.ResizeRoom(view.x + 8); yield return new WaitForFixedUpdate(); CheckNativeShapes(sandbox);
            sandbox.Controller.ResizeRoom(view.x); yield return new WaitForFixedUpdate(); CheckNativeShapes(sandbox);
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None),Is.Empty);
            Assert.That(Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None),Is.Empty);
            Assert.That(sandbox.pixelCamera.rect,Is.EqualTo(new Rect(0,0,1,1)),"camera fills the Game View");
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
                    var viewport=sandbox.pixelCamera.pixelRect;
                    int scale=Mathf.Max(1,(int)viewport.width/sandbox.Frame.width);
                    Color32 expected=sandbox.Frame.GetPixel(0,sandbox.Frame.height-1);
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
            yield return SceneManager.LoadSceneAsync("PhaseDemo3");
            sandbox=Object.FindAnyObjectByType<PhaseSandbox>();
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.RightArrow));
            yield return new WaitForSeconds(25f / 60);
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.RightArrow,Key.UpArrow,Key.C));
            yield return new WaitForSeconds(40f / 60);
            InputSystem.QueueStateEvent(k,new KeyboardState());
            yield return new WaitForFixedUpdate();
            Assert.That(sandbox.Controller.gears[0].Lift,Is.GreaterThan(0)); CheckNativeShapes(sandbox);
            sandbox.Controller.Reset(); yield return new WaitForFixedUpdate(); CheckNativeShapes(sandbox);
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
            var p=sandbox.Controller.player;p.x=sandbox.Controller.Width-9;p.y=100;p.vx=90;p.vy=-30;p.dash=0;p.stamina=63;
            sandbox.Controller.phase=2;
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.RightArrow));
            double deadline=Time.realtimeSinceStartupAsDouble+5;
            while(SceneManager.GetActiveScene().name!="PhaseRoom2"&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("PhaseRoom2"));
            InputSystem.QueueStateEvent(k,new KeyboardState());
            sandbox=Object.FindAnyObjectByType<PhaseSandbox>();
            Assert.That(sandbox.Controller.player,Is.SameAs(p));Assert.That(sandbox.Controller.phase,Is.EqualTo(2));
            Assert.That(p.x,Is.LessThan(32));Assert.That(p.y,Is.LessThan(110));Assert.That(p.dash,Is.Zero);Assert.That(p.stamina,Is.EqualTo(63));
            Assert.That(sandbox.InTransition, Is.True);
            double frozen = sandbox.Controller.time;
            var start = sandbox.Frame.GetPixels32();
            Directory.CreateDirectory("TestResults/Web");
            File.WriteAllBytes("TestResults/Web/transition-start.png", sandbox.Frame.EncodeToPNG());
            deadline=Time.realtimeSinceStartupAsDouble+5;
            while(sandbox.InTransition && Time.realtimeSinceStartupAsDouble<deadline)
            {
                Assert.That(sandbox.Controller.time, Is.EqualTo(frozen), "movement freezes during the slide");
                if (!start.SequenceEqual(sandbox.Frame.GetPixels32()))
                    File.WriteAllBytes("TestResults/Web/transition-mid.png", sandbox.Frame.EncodeToPNG());
                yield return null;
            }
            Assert.That(sandbox.InTransition, Is.False);
            CollectionAssert.AreNotEqual(start, sandbox.Frame.GetPixels32(), "slide reaches the arriving room");
            File.WriteAllBytes("TestResults/Web/transition-arrived.png", sandbox.Frame.EncodeToPNG());
            yield return new WaitForSeconds(.15f);
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("PhaseRoom2"));
            p.x=sandbox.Controller.Width-8;p.y=149;p.vx=90;p.vy=0;
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.RightArrow));
            yield return new WaitForSeconds(.1f);
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("PhaseRoom2"));Assert.That(p.x,Is.EqualTo(sandbox.Controller.Width-8));
            InputSystem.QueueStateEvent(k,new KeyboardState());p.x=32;p.y=200;
            yield return new WaitForSeconds(.4f);
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("PhaseRoom2"));
            Assert.That(sandbox.Controller.deaths,Is.EqualTo(1));Assert.That(sandbox.Controller.player.x,Is.EqualTo(sandbox.spawn.x));
            yield return new WaitForFixedUpdate(); CheckNativeShapes(sandbox);
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

    static void CheckNativeShapes(PhaseSandbox sandbox)
    {
        void Match(BoxCollider2D shape, Block model, bool active = true)
        {
            Assert.That(shape.enabled, Is.EqualTo(active), "native phase/deferred solidity");
            Assert.That(shape.size, Is.EqualTo(new Vector2((float)model.w,(float)model.h)), "native collision size");
            if (!active) return;
            Assert.That(shape.bounds.min.x, Is.EqualTo(model.x).Within(.001));
            Assert.That(-shape.bounds.max.y, Is.EqualTo(model.y).Within(.001));
        }
        var s = sandbox.Controller; var terrain = sandbox.GetComponentsInChildren<PhaseTerrain>();
        for (int i = 0; i < terrain.Length; i++)
            Match(terrain[i].GetComponent<BoxCollider2D>(),s.blocks[i],s.blocks[i].ActiveInPhase(s.phase)&&!s.blocked.Contains(i));
        var mover = sandbox.GetComponentInChildren<PhaseMovingPlatform>();
        if (mover != null) Match(mover.GetComponent<BoxCollider2D>(),s.mover);
        var gears = sandbox.GetComponentsInChildren<PhaseClimbGear>();
        for (int i = 0; i < gears.Length; i++)
        {
            Match(gears[i].GetComponent<BoxCollider2D>(),s.gears[i].wall);
            Match(gears[i].transform.Find("Platform collider").GetComponent<BoxCollider2D>(),s.gears[i].platform);
        }
    }

}
