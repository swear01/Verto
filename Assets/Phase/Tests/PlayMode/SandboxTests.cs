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
            PhaseSandbox.ApplyControls(custom);
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
            PhaseSandbox.ApplyControls(custom);
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
            PhaseSandbox.ApplyControls(null); PhaseSandbox.ApplyControls(new string[3]);
            int invalidCase = 0;
            foreach (var invalid in new System.Action<string[]>[] {
                a => a[8] = "L", a => a[2] = "Escape", a => a[2] = a[3] = "",
                a => a[3] = "L", a => a[11] = "LeftShift", a => a[2] = "Delete", a => a[2] = "1"
            })
            {
                var candidate = (string[])custom.Clone(); invalid(candidate); PhaseSandbox.ApplyControls(candidate);
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
            PhaseSandbox.ApplyControls(defaults);
            if (current != null) current.SetControlsOpen("false");
            InputSystem.RemoveDevice(k);
        }
    }


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

    #if UNITY_EDITOR
    [UnityTest]
    public IEnumerator MultipleAuthoredPlatformsReceiveKeyboardDashAndSyncTheirColliders()
    {
        yield return SceneManager.LoadSceneAsync("PhaseSandbox");
        var original=Object.FindAnyObjectByType<PhaseSandbox>();
        var root=new GameObject("Multi-platform probe");root.SetActive(false);
        var cameraParent=original.pixelCamera.transform.parent;
        root.AddComponent<Grid>().cellSize=new Vector3(8,8,1);root.AddComponent<PhaseGrid>();
        foreach(var route in new[]{new[]{new Vector2Int(80,144),new Vector2Int(80,80)},
            new[]{new Vector2Int(216,112),new Vector2Int(272,112)}})
        {
            var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Phase/Prefabs/"+
                (route[0].x==80?"DashPlatformVertical":"DashPlatformHorizontal")+".prefab");
            Assert.That(asset,Is.Not.Null);
            var anchor=(GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(asset,root.transform);
            anchor.transform.position=new Vector3(route[0].x,-route[0].y);
            var platform=anchor.GetComponentInChildren<PhaseMovingPlatform>();platform.nodes=new[]{Vector2Int.zero,route[1]-route[0]};
            platform.size=route[0].x==80?new Vector2Int(80,8):new Vector2Int(32,8);
            platform.returns=route[0].x==80;
        }
        var sandbox=root.AddComponent<PhaseSandbox>();sandbox.pixelCamera=original.pixelCamera;
        sandbox.pixelMaterial=original.pixelMaterial;sandbox.spawn=new Vector2Int(96,133);
        if(original.pixelCamera.transform.IsChildOf(original.transform))original.pixelCamera.transform.SetParent(null,true);
        var k=InputSystem.AddDevice<Keyboard>();
        var background=Application.runInBackground;var capture=Time.captureDeltaTime;
        var focus=InputSystem.settings.backgroundBehavior;var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        try
        {
            Application.runInBackground=true;Time.captureDeltaTime=1f/60;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            original.gameObject.SetActive(false);root.SetActive(true);sandbox.SetControlsOpen("false");
            InputSystem.QueueStateEvent(k,new KeyboardState());yield return null;yield return null;
            Assert.That(sandbox.Controller.movers.Length,Is.EqualTo(2));
            int frame=Time.frameCount;double time=sandbox.Controller.time;
            Directory.CreateDirectory("TestResults/MultiPlatforms");
            File.WriteAllBytes("TestResults/MultiPlatforms/start.png",sandbox.Frame.EncodeToPNG());
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.X,Key.RightArrow));yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(k,new KeyboardState());
            double deadline=Time.realtimeSinceStartupAsDouble+10;
            while((sandbox.Controller.movers[0].y!=80||sandbox.Controller.movers[1].x!=272)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(Time.frameCount,Is.GreaterThan(frame));Assert.That(sandbox.Controller.time,Is.GreaterThan(time));
            Assert.That(sandbox.Controller.dashes,Is.EqualTo(1));
            Assert.That(sandbox.Controller.movers[0].y,Is.EqualTo(80));
            Assert.That(sandbox.Controller.movers[1].x,Is.EqualTo(272));
            Assert.That(sandbox.Controller.player.y+sandbox.Controller.player.h,Is.EqualTo(80).Within(.01));
            yield return new WaitForFixedUpdate();CheckNativeShapes(sandbox);
            File.WriteAllBytes("TestResults/MultiPlatforms/forward.png",sandbox.Frame.EncodeToPNG());
            yield return new WaitForSeconds(1.6f);yield return new WaitForFixedUpdate();
            Assert.That(sandbox.Controller.movers[0].y,Is.EqualTo(144));
            Assert.That(sandbox.Controller.player.y+sandbox.Controller.player.h,Is.EqualTo(144).Within(.01));
            Assert.That(sandbox.Controller.deaths,Is.Zero);CheckNativeShapes(sandbox);
            File.WriteAllBytes("TestResults/MultiPlatforms/returned.png",sandbox.Frame.EncodeToPNG());
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            InputSystem.RemoveDevice(k);InputSystem.settings.backgroundBehavior=focus;
            InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            Application.runInBackground=background;Time.captureDeltaTime=capture;
            if(original!=null){original.pixelCamera.transform.SetParent(cameraParent,true);original.gameObject.SetActive(true);}
            Object.Destroy(root);
        }
    }

    #endif

    [UnityTest]
    public IEnumerator BigRoomCameraFollowsAKeyboardRunAndStaysInsideTheRoom()
    {
        yield return SceneManager.LoadSceneAsync("PhaseSandbox");
        var original=Object.FindAnyObjectByType<PhaseSandbox>();
        var root=new GameObject("Big room probe");root.SetActive(false);
        var cameraParent=original.pixelCamera.transform.parent;
        root.AddComponent<Grid>().cellSize=new Vector3(8,8,1);root.AddComponent<PhaseGrid>();
        var tile=new GameObject("Floor");tile.transform.SetParent(root.transform);tile.transform.position=new Vector3(0,-344);
        tile.AddComponent<PhaseTerrain>().size=new Vector2Int(640,16);
        var sandbox=root.AddComponent<PhaseSandbox>();sandbox.pixelCamera=original.pixelCamera;sandbox.pixelMaterial=original.pixelMaterial;
        sandbox.spawn=new Vector2Int(16,333);sandbox.roomSize=new Vector2Int(640,360);
        if(original.pixelCamera.transform.IsChildOf(original.transform))original.pixelCamera.transform.SetParent(null,true);
        var k=InputSystem.AddDevice<Keyboard>();
        var background=Application.runInBackground;var capture=Time.captureDeltaTime;
        var focus=InputSystem.settings.backgroundBehavior;var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        Vector2Int View(){var at=sandbox.pixelCamera.transform.position;return new Vector2Int(Mathf.RoundToInt(at.x-sandbox.Frame.width/2f),Mathf.RoundToInt(-at.y-sandbox.Frame.height/2f));}
        try
        {
            Application.runInBackground=true;Time.captureDeltaTime=1f/60;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            original.gameObject.SetActive(false);root.SetActive(true);sandbox.SetControlsOpen("false");
            InputSystem.QueueStateEvent(k,new KeyboardState());yield return null;yield return null;
            var size=new Vector2Int(sandbox.Frame.width,sandbox.Frame.height);
            Assert.That((sandbox.Controller.Width,sandbox.Controller.Height),Is.EqualTo((640,360)));
            Assert.That(View(),Is.EqualTo(new Vector2Int(0,Mathf.Max(0,360-size.y))),"the view starts clamped to the bottom-left");
            int frame=Time.frameCount;double time=sandbox.Controller.time;
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.RightArrow));
            for(int i=0;i<150;i++){yield return null;var v=View();Assert.That(v.x,Is.InRange(0,640-size.x));Assert.That(v.y,Is.InRange(Mathf.Min(0,(360-size.y)/2),Mathf.Max(0,360-size.y)));}
            InputSystem.QueueStateEvent(k,new KeyboardState());
            Assert.That(Time.frameCount,Is.GreaterThan(frame));Assert.That(sandbox.Controller.time,Is.GreaterThan(time));
            Assert.That(sandbox.Controller.player.x,Is.GreaterThan(200));Assert.That(View().x,Is.GreaterThan(0),"the view follows the run");
            sandbox.Controller.player.x=624;
            yield return new WaitForSeconds(2);
            Assert.That(View().x,Is.EqualTo(640-size.x),"the view stops at the room's right edge");
            Assert.That(sandbox.Controller.deaths,Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            InputSystem.RemoveDevice(k);InputSystem.settings.backgroundBehavior=focus;
            InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            Application.runInBackground=background;Time.captureDeltaTime=capture;
            if(original!=null){original.pixelCamera.transform.SetParent(cameraParent,true);original.gameObject.SetActive(true);}
            Object.Destroy(root);
        }
    }

    [UnityTest]
    public IEnumerator CrystalNativeTriggerTracksPhasePickupAndReset()
    {
        yield return SceneManager.LoadSceneAsync("PhaseCrystal1");
        var sandbox = Object.FindAnyObjectByType<PhaseSandbox>();
        sandbox.SetControlsOpen("true");
        try
        {
            var s = sandbox.Controller;
            var crystal = s.crystals.Single(c => c.Mask == 2);
            var pickup = sandbox.GetComponentsInChildren<PhaseCrystal>().Single(c => c.phaseMask == 2);
            var shape = pickup.GetComponent<BoxCollider2D>();
            yield return null; yield return null;
            Assert.That(s.phase, Is.Zero); Assert.That(shape, Is.Not.Null);
            Assert.That(shape.enabled, Is.False, "blue pickup is a ghost in red");
            Assert.That(shape.isTrigger, Is.True); Assert.That(shape.size, Is.EqualTo(new Vector2(8, 8)));

            s.phase = 1; yield return null; yield return null;
            Assert.That(shape.enabled, Is.True); CheckNativeShapes(sandbox);
            s.player.x = crystal.x; s.player.y = crystal.y; s.Step(default);
            yield return null; yield return null;
            Assert.That(crystal.collected, Is.True); Assert.That(s.player.airJump, Is.EqualTo(1));
            Assert.That(shape.enabled, Is.False, "consumed pickup has no native trigger");

            s.Reset(); s.phase = 1; yield return null; yield return null;
            Assert.That(crystal.collected, Is.False); Assert.That(shape.enabled, Is.True);
            CheckNativeShapes(sandbox); LogAssert.NoUnexpectedReceived();
        }
        finally { if (sandbox != null) sandbox.SetControlsOpen("false"); }
    }

    [System.Serializable] sealed class RouteStep { public int frames,x,y; public bool jump,dash,grab; }
    [System.Serializable] sealed class RouteSteps { public RouteStep[] steps; }

    static IEnumerator WaitForScene(string name)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 5;
        while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(name));
        yield return null;
    }
    static IEnumerator WaitForSlide(PhaseSandbox sandbox)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 5;
        while (sandbox.InTransition && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Assert.That(sandbox.InTransition, Is.False);
    }

    [UnityTest]
    public IEnumerator SelectionDuringPausedArrivalIsAcknowledgedOncePerRequest()
    {
        yield return SceneManager.LoadSceneAsync("PhaseDemo2");
        string previous = PhaseSandbox.Levels[System.Array.IndexOf(PhaseSandbox.Levels, "PhaseDemo2") - 1];
        var k = InputSystem.AddDevice<Keyboard>();
        UnityEngine.Events.UnityAction<Scene, LoadSceneMode> pauseArrival = (scene, mode) =>
        { if (scene.name == previous) Object.FindAnyObjectByType<PhaseSandbox>().SetControlsOpen("true"); };
        SceneManager.sceneLoaded += pauseArrival;
        try
        {
            var sandbox = Object.FindAnyObjectByType<PhaseSandbox>();
            var p = sandbox.Controller.player; p.x = .5; p.vx = -90;
            InputSystem.QueueStateEvent(k, new KeyboardState(Key.LeftArrow));
            yield return WaitForScene(previous);
            InputSystem.QueueStateEvent(k, new KeyboardState());
            sandbox = Object.FindAnyObjectByType<PhaseSandbox>();
            Assert.That(sandbox.InTransition, Is.True);
            sandbox.SetControlsOpen("true");
            yield return new WaitForSeconds(PhaseSandbox.TransitionDuration * 2);
            Assert.That(sandbox.InTransition, Is.True, "a dialog pauses the arrival slide");
            int count = PhaseSandbox.MapResultCount;
            foreach (var room in new[] { "5", "5", "13" }.Select(n => (n, PhaseSandbox.Levels[int.Parse(n) - 1])))
            {
                sandbox.SetControlsOpen("true"); sandbox.SelectDemoRoom(room.Item1);
                Assert.That(PhaseSandbox.MapResultCount, Is.EqualTo(count), "success waits for the async load");
                double deadline = Time.realtimeSinceStartupAsDouble + 5;
                while (PhaseSandbox.MapResultCount == count && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(PhaseSandbox.MapResultCount, Is.EqualTo(++count)); Assert.That(PhaseSandbox.LastMapError, Is.Empty);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(room.Item2)); yield return null;
                sandbox = Object.FindAnyObjectByType<PhaseSandbox>();
                Assert.That(Object.FindObjectsByType<PhaseSandbox>().Length, Is.EqualTo(1));
            }
            sandbox.SelectDemoRoom("3"); sandbox.SelectDemoRoom("4");
            Assert.That(PhaseSandbox.MapResultCount, Is.EqualTo(++count)); Assert.That(PhaseSandbox.LastMapError, Is.Not.Empty, "overlapping request is refused");
            yield return WaitForScene(PhaseSandbox.Levels[2]);
            Assert.That(PhaseSandbox.MapResultCount, Is.EqualTo(++count)); Assert.That(PhaseSandbox.LastMapError, Is.Empty);
            sandbox = Object.FindAnyObjectByType<PhaseSandbox>();
            double time = sandbox.Controller.time;
            yield return new WaitForSeconds(.2f);
            Assert.That(sandbox.Controller.time, Is.GreaterThan(time), "play resumes after the acknowledged load");
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            SceneManager.sceneLoaded -= pauseArrival;
            InputSystem.RemoveDevice(k);
            var current = Object.FindAnyObjectByType<PhaseSandbox>(); if (current != null) current.SetControlsOpen("false");
        }
    }

    [UnityTest]
    public IEnumerator BackwardTravelEntersEveryPreviousLevelAtASafeRightCheckpoint()
    {
        var levels = PhaseSandbox.Levels;
        yield return SceneManager.LoadSceneAsync(levels[levels.Length - 1]);
        var k = InputSystem.AddDevice<Keyboard>();
        try
        {
            var sandbox = Object.FindAnyObjectByType<PhaseSandbox>();
            Assert.That(sandbox.leftScene, Is.EqualTo(levels[levels.Length - 2]));
            for (int i = levels.Length - 2; i >= 0; i--)
            {
                var before = sandbox.Controller; int deaths = before.deaths;
                before.phase = 0; var p = before.player; p.x = .5; p.y = sandbox.spawn.y; p.vx = -90; p.vy = 0;
                InputSystem.QueueStateEvent(k, new KeyboardState(Key.LeftArrow));
                yield return WaitForScene(levels[i]);
                InputSystem.QueueStateEvent(k, new KeyboardState());
                sandbox = Object.FindAnyObjectByType<PhaseSandbox>();
                var s = sandbox.Controller;
                Assert.That(s.player.x, Is.InRange(s.Width / 2, s.Width - 8), levels[i] + " enters at the right");
                var checkpoint = s.phase == 0 ? (s.player.x, s.player.y) : (sandbox.spawn.x, sandbox.spawn.y);
                Assert.That((s.spawnX, s.spawnY), Is.EqualTo(checkpoint), levels[i] + " respawns on terrain solid in red");
                Assert.That(s.player.facing, Is.EqualTo(-1));
                Assert.That(s.blocks.Any(b => b.ActiveInPhase(s.phase) && b.y == s.player.y + s.player.h && b.x <= s.player.x && b.x + b.w >= s.player.x + s.player.w),
                    Is.True, levels[i] + " entry stands on terrain");
                Assert.That(s.deaths, Is.EqualTo(deaths)); Assert.That(s.time, Is.GreaterThanOrEqualTo(before.time));
                Assert.That(sandbox.rightScene, Is.EqualTo(levels[i + 1]));
                yield return WaitForSlide(sandbox);
                double x = s.player.x;
                yield return new WaitForSeconds(.5f);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(levels[i]), levels[i] + " does not bounce forward");
                Assert.That((s.deaths, s.player.x), Is.EqualTo((deaths, x)), levels[i] + " entry is stable");
            }
            var first = sandbox.Controller; first.phase = 0; first.player.x = .5; first.player.y = sandbox.spawn.y; first.player.vx = -90;
            InputSystem.QueueStateEvent(k, new KeyboardState(Key.LeftArrow)); yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(k, new KeyboardState());
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(levels[0])); Assert.That(first.player.x, Is.Zero, "level1 left edge stays closed");
            LogAssert.NoUnexpectedReceived();
        }
        finally { InputSystem.RemoveDevice(k); }
    }

    [TestCase(320)] [TestCase(427)] [TestCase(640)]
    public void RightArrivalUsesTheViewportEdgeAndIgnoresInactiveHazards(int width)
    {
        int right = (width - 16) / 8 * 8;
        var c = new PhaseController(new[] { new Block(0,160,320,8,phaseMask:2) },24,149,
            spikes:new[] { new Spike(right,144,8,SpikeDirection.Up,1) },
            lasers:new[] { new Laser(right,144,LaserDirection.Down,1) });
        c.ResizeRoom(width);
        var entry = PhaseSandbox.RightEntry(c,149);
        Assert.That(entry.HasValue,Is.True);
        Assert.That(entry.Value.at,Is.EqualTo(new Vector2Int(right,149)));
        Assert.That(entry.Value.phase,Is.EqualTo(1));
        Assert.That(c.phase,Is.Zero,"searching must preserve the controller phase");
    }

    [UnityTest]
    public IEnumerator LastLevelRightExitWinsAndJumpRestartsLevelOne()
    {
        string previous = PhaseSandbox.Levels[PhaseSandbox.Levels.Length - 2], last = PhaseSandbox.Levels[PhaseSandbox.Levels.Length - 1];
        yield return SceneManager.LoadSceneAsync(previous);
        var k = InputSystem.AddDevice<Keyboard>();
        try
        {
            IEnumerator ExitRight(PhaseSandbox room)
            {
                var c = room.Controller;
                var floor = c.blocks.Where(b => b.x < c.Width && b.x + b.w >= c.Width && b.y >= 11).OrderBy(b => b.y).FirstOrDefault();
                Assert.That(floor, Is.Not.Null, room.gameObject.scene.name + " has an authored edge landing");
                c.phase = System.Array.FindIndex(new[] { 0,1,2 }, floor.ActiveInPhase);
                c.player.x = c.Width - c.player.w - .5; c.player.y = floor.y - c.player.h; c.player.vx = 90; c.player.vy = 0;
                InputSystem.QueueStateEvent(k, new KeyboardState(Key.RightArrow)); yield return null; yield return null;
            }
            var sandbox = Object.FindAnyObjectByType<PhaseSandbox>();
            Assert.That(sandbox.rightScene, Is.EqualTo(last));
            yield return ExitRight(sandbox);
            yield return WaitForScene(last);
            InputSystem.QueueStateEvent(k, new KeyboardState());
            sandbox = Object.FindAnyObjectByType<PhaseSandbox>();
            Assert.That(sandbox.Controller.player.x, Is.EqualTo(sandbox.spawn.x), "forward entry keeps the authored left spawn");
            Assert.That(sandbox.Won, Is.False);
            yield return WaitForSlide(sandbox);
            float captureDelta = Time.captureDeltaTime;
            int deaths = sandbox.Controller.deaths;
            try
            {
                Time.captureDeltaTime = (float)PhaseController.Dt;
                InputSystem.QueueStateEvent(k,new KeyboardState()); yield return null;
                sandbox.Controller.Reset();
                // A level without a required route still checks the victory by walking off the authored edge landing.
                bool unrouted = PhaseRoute.UnroutedReason(last) != null;
                if (unrouted) yield return ExitRight(sandbox);
                var route = unrouted ? new RouteStep[0] : JsonUtility.FromJson<RouteSteps>("{\"steps\":" + File.ReadAllText("Verification/Routes/" + last + ".json") + "}").steps;
                foreach (var step in route)
                {
                    var keys = new System.Collections.Generic.List<Key>();
                    if (step.x != 0) keys.Add(step.x < 0 ? Key.LeftArrow : Key.RightArrow);
                    if (step.y != 0) keys.Add(step.y < 0 ? Key.UpArrow : Key.DownArrow);
                    if (step.jump) keys.Add(Key.Z); if (step.dash) keys.Add(Key.X); if (step.grab) keys.Add(Key.C);
                    InputSystem.QueueStateEvent(k,new KeyboardState(keys.ToArray()));
                    for (int n = 0; n < step.frames && !sandbox.Won; n++) yield return null;
                    if (sandbox.Won) break;
                }
                InputSystem.QueueStateEvent(k,new KeyboardState(Key.RightArrow));
                double until = Time.realtimeSinceStartupAsDouble + 5;
                while (!sandbox.Won && sandbox.Controller.deaths == deaths && Time.realtimeSinceStartupAsDouble < until) yield return null;
                Assert.That(sandbox.Controller.deaths,Is.EqualTo(deaths),"full last-level keyboard route has no deaths");
            }
            finally { Time.captureDeltaTime = captureDelta; }
            double deadline = Time.realtimeSinceStartupAsDouble + 2;
            while (!sandbox.Won && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            InputSystem.QueueStateEvent(k, new KeyboardState());
            Assert.That(sandbox.Won, Is.True, last + " right edge wins");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(last));
            double time = sandbox.Controller.time; yield return new WaitForSeconds(.2f);
            Assert.That(sandbox.Controller.time, Is.EqualTo(time), "victory freezes play");
            var light = new Color32(234, 223, 197, 255);
            Assert.That(sandbox.Frame.GetPixels32().Count(c => c.Equals(light)), Is.GreaterThan(150), "native frame shows the victory banner");
            Directory.CreateDirectory("TestResults/Navigation");
            File.WriteAllBytes("TestResults/Navigation/victory.png", sandbox.Frame.EncodeToPNG());
            InputSystem.QueueStateEvent(k, new KeyboardState(Key.Z)); yield return null;
            InputSystem.QueueStateEvent(k, new KeyboardState());
            yield return WaitForScene("PhaseDemo1");
            sandbox = Object.FindAnyObjectByType<PhaseSandbox>();
            Assert.That(sandbox.Won, Is.False); Assert.That(sandbox.Controller.player.x, Is.EqualTo(sandbox.spawn.x));
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
        var movers = sandbox.GetComponentsInChildren<PhaseMovingPlatform>();
        Assert.That(movers.Length, Is.EqualTo(s.movers.Length));
        for (int i = 0; i < movers.Length; i++) Match(movers[i].GetComponent<BoxCollider2D>(),s.movers[i]);
        var gears = sandbox.GetComponentsInChildren<PhaseClimbGear>();
        for (int i = 0; i < gears.Length; i++)
        {
            Match(gears[i].GetComponent<BoxCollider2D>(),s.gears[i].wall);
            Match(gears[i].transform.Find("Platform collider").GetComponent<BoxCollider2D>(),s.gears[i].platform);
        }
        var crystals = sandbox.GetComponentsInChildren<PhaseCrystal>();
        for (int i = 0; i < crystals.Length; i++)
            Match(crystals[i].GetComponent<BoxCollider2D>(),s.crystals[i],s.crystals[i].ActiveInPhase(s.phase)&&!s.crystals[i].collected);
    }

}
