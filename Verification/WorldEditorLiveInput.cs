using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// With PhaseTitle in Play: opens the editor, paints a second screen beside the first, merges the two rooms with the room tool,
// splits them again, places a spawn and a goal, then playtests from the first room and walks across the seam into the goal.
// Restores the developer's draft and clipboard; frames go to TestResults/WorldEditor/.
public static class WorldEditorLiveInput
{
    const string Reports = "TestResults/WorldEditor/";
    static async Task Frames(int count)
    {
        int target = Time.frameCount + count; double deadline = Time.realtimeSinceStartupAsDouble + 30;
        while (Time.frameCount < target && Time.realtimeSinceStartupAsDouble < deadline) await Task.Delay(8);
        if (Time.frameCount < target) throw new TimeoutException("Player loop did not advance.");
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static async Task Tap(Keyboard keyboard, params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); await Frames(2);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
    }
    static async Task Point(Mouse mouse, Vector2Int view, Vector2Int frame, params MouseButton[] buttons)
    {
        var state = new MouseState { position = new Vector2((frame.x + .5f) * Screen.width / view.x, Screen.height - (frame.y + .5f) * Screen.height / view.y) };
        foreach (var button in buttons) state = state.WithButton(button);
        mouse.MakeCurrent(); InputSystem.QueueStateEvent(mouse, state); await Frames(2);
    }
    static async Task Scene(string name)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 15;
        while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartupAsDouble < deadline) await Task.Delay(16);
        Require(SceneManager.GetActiveScene().name == name, "Did not reach " + name); await Frames(3);
    }
    static void Capture(MonoBehaviour screen, string name) =>
        File.WriteAllBytes(Reports + name + ".png", screen.GetComponentsInChildren<SpriteRenderer>().First(r => r.enabled).sprite.texture.EncodeToPNG());
    public static async Task<string> Run()
    {
        Require(Application.isPlaying && SceneManager.GetActiveScene().name == PhaseTitle.Scene, "Open PhaseTitle in Play mode first.");
        bool background = Application.runInBackground; var focus = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode; float captureDelta = Time.captureDeltaTime;
        string saved = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null, clipboard = GUIUtility.systemCopyBuffer;
        Keyboard keyboard = null; Mouse mouse = null;
        var bindings = PhaseSandbox.Slots.ToArray(); PhaseSandbox.ApplyControls(PhaseSandbox.DefaultSlots);
        try
        {
            Directory.CreateDirectory(Reports);
            Application.runInBackground = true; Time.captureDeltaTime = (float)PhaseController.Dt;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            PlayerPrefs.DeleteKey(PhaseEditor.DraftKey);
            keyboard = InputSystem.AddDevice<Keyboard>("WorldEditorKeys"); mouse = InputSystem.AddDevice<Mouse>("WorldEditorMouse");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(3);
            int frame = Time.frameCount;
            await Tap(keyboard, Key.RightArrow); await Tap(keyboard, Key.RightArrow); await Tap(keyboard, Key.Z);
            await Scene(PhaseEditor.Scene);
            var editor = UnityEngine.Object.FindAnyObjectByType<PhaseEditor>(); var view = PhaseEditor.ViewFor(Screen.width, Screen.height);
            Vector2Int At(int x, int y) => new Vector2Int((x - editor.Scroll.x) * 8 + 4, (y - editor.Scroll.y) * 8 + 4 - (180 - view.y) / 2);
            Vector2Int Slot(int slot) { var r = PhaseEditor.SlotIn(view, slot); return new Vector2Int(r.x + r.width / 2, r.y + r.height / 2); }
            async Task Click(Vector2Int at) { await Point(mouse, view, at, MouseButton.Left); await Point(mouse, view, at); }
            async Task Pan(int cells)
            {
                var from = new Vector2Int(cells > 0 ? 300 : 20, 88); var to = from - new Vector2Int(cells * 8, 0);
                await Point(mouse, view, from, MouseButton.Middle); await Point(mouse, view, to, MouseButton.Middle); await Point(mouse, view, to);
            }
            Require(editor.RoomCount == 1, "A new draft is not one room.");
            // Paint the second screen: a floor under the first room's, so walking right keeps footing.
            await Pan(35); Require(editor.Scroll.x == 35, "The window did not scroll.");
            await Click(Slot(0));
            for (int x = 40; x < 48; x++) await Click(At(x, 21));
            Require(editor.RoomCount == 2 && editor.World().rooms[1].atX == 1, "Painting beside the room did not make a second room.");
            Capture(editor, "1-two-rooms");
            // The room tool joins them at the seam and splits them along it.
            await Click(Slot(9)); await Click(At(40, 10));
            Require(editor.RoomCount == 1 && editor.World().rooms[0].GridWidth == 640, "Merging did not make one 2x1 room.");
            Capture(editor, "2-merged");
            await Click(At(40, 10));
            Require(editor.RoomCount == 2, "Splitting did not make two rooms.");
            // A spawn and a goal in the second room.
            await Click(Slot(6)); await Click(At(44, 19));
            await Click(Slot(8)); await Click(At(46, 20));
            Require(!editor.World().rooms[1].noSpawn && editor.World().rooms[1].extraSpawns == null && editor.World().rooms[1].goal != null, "The second room did not get its spawn and goal.");
            Capture(editor, "3-spawn-goal");
            await Tap(keyboard, Key.Tab); Require(editor.Overview, "Tab did not show the overview."); Capture(editor, "4-overview"); await Tap(keyboard, Key.Tab);
            // Back at the first room: the playtest starts there, runs right across the seam and touches the goal.
            await Pan(-35); Require(editor.Scroll.x == 0, "The window did not scroll back.");
            await Tap(keyboard, Key.Enter);
            var sandbox = editor.Playtest; Require(sandbox != null, "The playtest did not start: flashing=" + editor.Flashing);
            var play = sandbox.World; Require(play.Room == 0, "The playtest did not start in the viewed room.");
            double time = sandbox.Controller.time; var seen = new System.Collections.Generic.List<int> { play.Room };
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            for (int i = 0; i < 1500 && !sandbox.Won; i++)
            {
                await Frames(1);
                if (play.Room != seen.Last()) { seen.Add(play.Room); await Frames(4); Capture(editor, "5-seam"); }
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
            Require(sandbox.Won && seen.SequenceEqual(new[] { 0, 1 }), "The run did not cross the seam into the goal: rooms " + string.Join(",", seen));
            Capture(editor, "6-goal");
            string report = $"passed: painted, merged, split, spawn+goal, overview, playtest from room 0 across the seam to the goal; frames={Time.frameCount - frame}, controllerSeconds={sandbox.Controller.time - time:F3}";
            File.WriteAllText(Reports + "live-input.txt", report); return report;
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (saved == null) PlayerPrefs.DeleteKey(PhaseEditor.DraftKey); else PlayerPrefs.SetString(PhaseEditor.DraftKey, saved);
            PlayerPrefs.Save(); GUIUtility.systemCopyBuffer = clipboard; PhaseSandbox.ApplyControls(bindings);
            InputSystem.settings.backgroundBehavior = focus; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background; Time.captureDeltaTime = captureDelta;
        }
    }
}
