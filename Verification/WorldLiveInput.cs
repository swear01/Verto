using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// In PhaseSandbox Play mode: loads a world of four rooms (left, middle, right, and one above the middle), walks left to right
// across both seams into the goal, then reloads it, walks into the middle room and jumps up into the room above.
// Frames and a summary go to TestResults/World.
public static class WorldLiveInput
{
    static async Task Frames(int count)
    {
        int target = Time.frameCount + count; double deadline = Time.realtimeSinceStartupAsDouble + 30;
        while (Time.frameCount < target && Time.realtimeSinceStartupAsDouble < deadline) await Task.Delay(8);
    }
    static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    // A floor at the given row of pixels, near the top in the rooms beside the middle one so a jump from a step on it can leave
    // the middle room upward and land on the floor at the bottom of the room above.
    static PhaseDemoRoom Room(int x, int y, double floor, double? goal = null, params Block[] more) => new PhaseDemoRoom {
        atX = x, atY = y, spawnX = 24, spawnY = floor - 16, blocks = new[] { new Block(0, floor, 320, 8, phaseMask: 7) }.Concat(more).ToArray(),
        goal = goal == null ? null : new DemoPoint { x = goal.Value, y = 8 }
    };
    static void Capture(string directory, PhaseSandbox sandbox, string name) =>
        File.WriteAllBytes(Path.Combine(directory, name + ".png"), sandbox.Frame.EncodeToPNG());
    public static async Task<string> Run()
    {
        var sandbox = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
        if (!Application.isPlaying || sandbox == null) throw new InvalidOperationException("Open PhaseSandbox in Play mode.");
        const string directory = "TestResults/World"; Directory.CreateDirectory(directory);
        bool background = Application.runInBackground; var focus = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode; Keyboard keyboard = null; float captureDelta = Time.captureDeltaTime;
        var slots = PhaseSandbox.Slots.ToArray();
        try
        {
            Application.runInBackground = true; Time.captureDeltaTime = (float)PhaseController.Dt;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Require(PhaseSandbox.ApplyControls(PhaseSandbox.DefaultSlots), "The default keys were refused.");
            keyboard = InputSystem.AddDevice<Keyboard>("WorldVerification");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(3);
            string walk = PhaseCustomMap.ToJson(new PhaseWorld { name = "Walk", rooms = new[] { Room(-1, 0, 16), Room(0, 0, 16), Room(1, 0, 16, 240), Room(0, -1, 168) } });
            string climb = PhaseCustomMap.ToJson(new PhaseWorld { name = "Climb", rooms = new[] { Room(-1, 0, 16), Room(0, 0, 16, null, new Block(48, 8, 80, 8, phaseMask: 7)), Room(1, 0, 16, 240), Room(0, -1, 168) } });
            int frame = Time.frameCount;

            sandbox.LoadCustomMap(walk); Require(PhaseSandbox.LastMapError == "", PhaseSandbox.LastMapError); await Frames(3);
            sandbox = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>(); var play = sandbox.World;
            Require(play != null && play.world.rooms.Length == 4 && play.Room == 0, "The world did not start in its leftmost room.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            double seconds = play.Controller.time; var seen = new System.Collections.Generic.List<int> { play.Room };
            for (int i = 0; i < 1200 && !sandbox.Won; i++)
            {
                await Frames(1);
                if (play.Room != seen.Last()) { seen.Add(play.Room); if (seen.Count == 2) { await Frames(5); Capture(directory, sandbox, "seam"); } }
            }
            Require(sandbox.Won && seen.SequenceEqual(new[] { 0, 1, 2 }), "Walking right did not cross both seams into the goal: rooms " + string.Join(",", seen));
            Capture(directory, sandbox, "goal");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
            double crossed = play.Controller.time - seconds;

            sandbox.LoadCustomMap(climb); Require(PhaseSandbox.LastMapError == "", PhaseSandbox.LastMapError); await Frames(3);
            sandbox = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>(); play = sandbox.World;
            Require(!sandbox.Won && play.Room == 0, "The reloaded world did not restart.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            for (int i = 0; i < 600 && play.Room != 1; i++) await Frames(1);
            Require(play.Room == 1, "Did not reach the middle room.");
            for (int i = 0; i < 40 && play.pause > 0; i++) await Frames(1);
            for (int i = 0; i < 60 && !play.Controller.player.grounded; i++) await Frames(1);
            await Frames(20);
            // Up onto the step with Right held, then straight up through the top of the room.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Z, Key.RightArrow)); await Frames(2);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            for (int i = 0; i < 90 && !(play.Controller.player.grounded && play.Controller.player.y + play.Controller.player.h == 8); i++) await Frames(1);
            Require(play.Room == 1 && play.Controller.player.y + play.Controller.player.h == 8, "Did not land on the step.");
            await Frames(3); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Z));
            for (int i = 0; i < 40 && play.Room != 3; i++) await Frames(1);
            Require(play.Room == 3, "Jumping did not enter the room above.");
            // Keep holding the jump through the pause so the rise is not cut short.
            for (int i = 0; i < 40 && play.pause > 0; i++) await Frames(1);
            await Frames(8); InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Frames(3); Capture(directory, sandbox, "above-slide");
            for (int i = 0; i < 90 && !(play.Room == 3 && play.Controller.player.grounded); i++) await Frames(1);
            await Frames(10); Capture(directory, sandbox, "above");
            var c = play.Controller; var p = c.player;
            string state = $"rooms={string.Join(",", seen)}+1,3, above: room={play.Room} y={p.y:F1} grounded={p.grounded} phase={c.phase} respawnPhase={c.respawnPhase} jumps={c.jumps} deaths={c.deaths} " +
                $"spawn=({c.spawnX},{c.spawnY}), frames={Time.frameCount - frame}, controllerSeconds={c.time:F3} (first walk {crossed:F3})";
            Require(play.Room == 3 && p.grounded && c.deaths == 0 && c.jumps == 2 && c.phase == 2 && c.respawnPhase == 2 && c.time > 0 && Time.frameCount > frame + 100, "The upper room state is wrong: " + state);
            File.WriteAllText(Path.Combine(directory, "live-input.txt"), "passed: " + state);
            return "passed: " + state;
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            PhaseSandbox.ApplyControls(slots);
            InputSystem.settings.backgroundBehavior = focus; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background; Time.captureDeltaTime = captureDelta;
        }
    }
}
