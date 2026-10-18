using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public static class DemoLiveInput
{
    [Serializable] public sealed class Routes { public Room[] rooms; }
    [Serializable] public sealed class Room { public Step[] route; public Step[][] bypassRoutes; public Goal goal; public string suffix; }
    [Serializable] public sealed class Step { public int frames,x,y; public bool jump,dash,grab; }
    [Serializable] public sealed class Goal { public double x,y,lift; public int phase,jumps,dashes; }

    public static Task<string> Run() => RunRoom(false);
    public static Task<string> Bypass() => RunRoom(true);
    static async Task<string> RunRoom(bool bypass)
    {
        var sandbox = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        string scene = SceneManager.GetActiveScene().name;
        string prefix = scene.StartsWith("PhaseCrystal") ? "PhaseCrystal" : "PhaseDemo";
        string course = prefix == "PhaseCrystal" ? "CrystalRooms" : "DemoRooms";
        if (!Application.isPlaying || sandbox == null || !scene.StartsWith(prefix) || !int.TryParse(scene.Substring(prefix.Length),out int number))
            throw new InvalidOperationException("Open a demo or crystal scene in Play mode.");
        var room = Newtonsoft.Json.JsonConvert.DeserializeObject<Routes>(File.ReadAllText("Assets/Phase/"+course+".json")).rooms[number-1];
        var routes = bypass ? room.bypassRoutes : new[] { room.route };
        if (routes == null || routes.Length == 0) throw new InvalidOperationException("This room has no recorded bypass routes.");
        var reports = new System.Collections.Generic.List<string>();
        for (int i = 0; i < routes.Length; i++)
        {
            // Awaiters must start the next fixture after this coroutine restores timing/input.
            var result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var trial = new Room { route = routes[i], goal = bypass ? null : room.goal, suffix = bypass ? "-bypass-"+(i+1) : "" };
            float beforeCapture=Time.captureDeltaTime;bool beforeBackground=Application.runInBackground;
            var beforeFocus=InputSystem.settings.backgroundBehavior;
            var beforeEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            UnityEngine.Events.UnityAction<Scene,Scene> changed = (a,b) => result.TrySetException(new InvalidOperationException("Route crossed the exit before its landing check: "+scene));
            SceneManager.activeSceneChanged += changed;
            try { sandbox.StartCoroutine(Check(sandbox,trial,number,course,result)); reports.Add(await result.Task); }
            finally { SceneManager.activeSceneChanged -= changed; }
            if(Time.captureDeltaTime!=beforeCapture||Application.runInBackground!=beforeBackground||
                InputSystem.settings.backgroundBehavior!=beforeFocus||InputSystem.settings.editorInputBehaviorInPlayMode!=beforeEditorInput)
                throw new InvalidOperationException("Fixture timing/input was not restored before the next route.");
        }
        return string.Join("\n", reports);
    }
    static IEnumerator Check(PhaseSandbox sandbox, Room room, int number, string course, TaskCompletionSource<string> result)
    {
        bool background = Application.runInBackground;
        var focus = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        float captureDelta = Time.captureDeltaTime;
        Keyboard keyboard = null;
        try
        {
            Application.runInBackground = true; Time.captureDeltaTime = (float)PhaseController.Dt;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return null;
            keyboard = InputSystem.AddDevice<Keyboard>("PhaseDemoVerification");
            var s = sandbox.Controller; s.Reset();
            string label = number + room.suffix;
            int deaths = s.deaths, jumps = s.jumps, dashes = s.dashes, frame = Time.frameCount;
            double time = s.time;
            Directory.CreateDirectory("TestResults/"+course);
            File.WriteAllBytes($"TestResults/{course}/{label}-start.png",sandbox.Frame.EncodeToPNG());
            bool landed = false;
            for (int i=0;i<room.route.Length && !landed;i++)
            {
                var step=room.route[i];
                var keys = new System.Collections.Generic.List<Key>();
                if (step.x < 0) keys.Add(Key.LeftArrow); else if (step.x > 0) keys.Add(Key.RightArrow);
                if (step.y < 0) keys.Add(Key.UpArrow); else if (step.y > 0) keys.Add(Key.DownArrow);
                if (step.jump) keys.Add(Key.Z); if (step.dash) keys.Add(Key.X); if (step.grab) keys.Add(Key.C);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys.ToArray()));
                for (int n = 0; n < step.frames; n++)
                {
                    yield return null;
                    if (room.goal != null && s.player.x>=room.goal.x && s.player.grounded && s.player.y+s.player.h==room.goal.y &&
                        s.phase==room.goal.phase && s.jumps-jumps==room.goal.jumps && s.dashes-dashes==room.goal.dashes)
                    { landed = true; break; }
                }
            }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            var p = s.player; var goal = room.goal;
            if (goal == null)
            {
                bool failed = course == "CrystalRooms" ? s.deaths <= deaths : s.deaths != deaths || s.gears[0].Lift != 0 || p.x+p.w > 80;
                if (failed || Time.frameCount <= frame || s.time <= time)
                {
                    result.TrySetException(new InvalidOperationException($"Room {label} bypass escaped: x={p.x}, deaths={s.deaths-deaths}"));
                    yield break;
                }
                File.WriteAllBytes($"TestResults/{course}/{label}-end.png",sandbox.Frame.EncodeToPNG());
                string blocked = $"Room {label} blocked: frames={Time.frameCount-frame}, seconds={s.time-time:F3}, x={p.x}, deaths={s.deaths-deaths}";
                File.WriteAllText($"TestResults/{course}/{label}.txt",blocked); result.SetResult(blocked); yield break;
            }
            if (s.deaths != deaths || p.x < goal.x || p.y + p.h != goal.y || !p.grounded || s.phase != goal.phase ||
                s.jumps-jumps != goal.jumps || s.dashes-dashes != goal.dashes || (s.gears.Length > 0 && s.gears[0].Lift != goal.lift) ||
                Time.frameCount <= frame || s.time <= time)
            {
                result.TrySetException(new InvalidOperationException($"Room {number}: x={p.x}, feet={p.y+p.h}, phase={s.phase}, jumps={s.jumps-jumps}, dashes={s.dashes-dashes}, deaths={s.deaths-deaths}, lift={(s.gears.Length>0?s.gears[0].Lift:0)}"));
                yield break;
            }
            File.WriteAllBytes($"TestResults/{course}/{label}-end.png",sandbox.Frame.EncodeToPNG());
            string report = $"Room {number} passed: frames={Time.frameCount-frame}, seconds={s.time-time:F3}, jumps={s.jumps-jumps}, dashes={s.dashes-dashes}, deaths=0";
            File.WriteAllText($"TestResults/{course}/{label}.txt",report);
            result.SetResult(report);
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = focus;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Time.captureDeltaTime = captureDelta; Application.runInBackground = background;
        }
    }
    public static async Task<string> Advance()
    {
        var before = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        if (!Application.isPlaying || before == null || string.IsNullOrEmpty(before.rightScene)) throw new InvalidOperationException("No next demo room.");
        string next = before.rightScene;
        int deaths=before.Controller.deaths, jumps=before.Controller.jumps, dashes=before.Controller.dashes;
        bool background=Application.runInBackground;
        var focus=InputSystem.settings.backgroundBehavior;
        var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        Keyboard keyboard=null;
        try
        {
            Application.runInBackground=true;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            int frame=Time.frameCount;
            while (Time.frameCount==frame) await Task.Delay(16);
            keyboard=InputSystem.AddDevice<Keyboard>("PhaseDemoTravelVerification");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));
            double deadline=Time.realtimeSinceStartupAsDouble+15;
            while (SceneManager.GetActiveScene().name!=next && Time.realtimeSinceStartupAsDouble<deadline) await Task.Delay(16);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            PhaseSandbox after;
            do { await Task.Delay(16); after=UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>(); }
            while ((after==null || after.InTransition) && Time.realtimeSinceStartupAsDouble<deadline);
            if (after==null || SceneManager.GetActiveScene().name!=next || after.InTransition || after.Controller.phase!=0 ||
                after.Controller.player.dash!=1 || after.Controller.deaths!=deaths || after.Controller.jumps!=jumps || after.Controller.dashes!=dashes)
                throw new InvalidOperationException("Demo checkpoint or scene travel failed: "+next);
            return "Checkpoint passed: "+next;
        }
        finally
        {
            if (keyboard!=null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior=focus;
            InputSystem.settings.editorInputBehaviorInPlayMode=editorInput; Application.runInBackground=background;
        }
    }
}
