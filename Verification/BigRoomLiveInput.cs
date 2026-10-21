using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Keyboard and mouse route: title → editor → grow the room to 80×38 with the panel arrows → scroll it with the cursor, the wheel,
// Ctrl+wheel and a middle-button drag → paint a floor across the new columns → export, reject a bad file, re-import → playtest
// and run into the scrolled part of the room while the camera follows. Restores the developer's draft and clipboard.
public static class BigRoomLiveInput
{
    const string Reports = "TestResults/BigRoom/";
    static async Task Frames(int count)
    {
        int target = Time.frameCount + count; double deadline = Time.realtimeSinceStartupAsDouble + 30;
        while (Time.frameCount < target && Time.realtimeSinceStartupAsDouble < deadline) await Task.Delay(16);
        if (Time.frameCount < target) throw new TimeoutException("Player loop did not advance.");
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static async Task Tap(Keyboard keyboard, params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); await Frames(2);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
    }
    // Points at a frame pixel's center, inverting PhaseScreen.FramePoint, with the given buttons held and the wheel turned.
    static async Task Point(Mouse mouse, Vector2Int view, Vector2Int frame, Vector2 wheel = default, params MouseButton[] buttons)
    {
        var state = new MouseState { position = new Vector2((frame.x + .5f) * Screen.width / view.x, Screen.height - (frame.y + .5f) * Screen.height / view.y), scroll = wheel };
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
        File.WriteAllBytes(Reports + name + ".png", ((Texture2D)screen.GetComponentsInChildren<SpriteRenderer>().First(r => r.enabled).sprite.texture).EncodeToPNG());
    public static async Task<string> Run()
    {
        Require(Application.isPlaying && SceneManager.GetActiveScene().name == PhaseTitle.Scene, "Open PhaseTitle in Play mode first.");
        bool background = Application.runInBackground;
        var focus = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        string saved = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null, clipboard = GUIUtility.systemCopyBuffer;
        Keyboard keyboard = null; Mouse mouse = null;
        // Other Editors of this project share PlayerPrefs, so the route uses the default keys in memory only.
        var bindings = PhaseSandbox.Slots.ToArray(); PhaseSandbox.ApplyControls(PhaseSandbox.DefaultSlots);
        try
        {
            Directory.CreateDirectory(Reports);
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            PlayerPrefs.DeleteKey(PhaseEditor.DraftKey);
            keyboard = InputSystem.AddDevice<Keyboard>("PhaseBigRoomVerification");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);

            await Tap(keyboard, Key.RightArrow); await Tap(keyboard, Key.RightArrow); await Tap(keyboard, Key.Space);
            await Scene(PhaseEditor.Scene);
            var editor = UnityEngine.Object.FindAnyObjectByType<PhaseEditor>();
            mouse = InputSystem.AddDevice<Mouse>("PhaseBigRoomVerificationMouse");
            var view = PhaseEditor.ViewFor(Screen.width, Screen.height);
            var texture = editor.GetComponentInChildren<SpriteRenderer>().sprite.texture;
            Vector2Int At(int x, int y) => new Vector2Int((x - editor.Scroll.x) * 8 + 4, (y - editor.Scroll.y) * 8 + 4 - (180 - view.y) / 2);
            Vector2Int Slot(int slot) { var r = PhaseEditor.SlotIn(view, slot); return new Vector2Int(r.x + r.width / 2, r.y + r.height / 2); }
            async Task Click(int slot) { await Point(mouse, view, Slot(slot), default, MouseButton.Left); await Point(mouse, view, Slot(slot)); }
            Require((editor.RoomColumns, editor.RoomRows) == (40, 22), "A new draft is not one screen.");
            Capture(editor, "01-one-screen");

            // Panel arrows: five wider and two taller make 80×38.
            for (int i = 0; i < 5; i++) await Click(PhaseEditor.WiderSlot);
            for (int i = 0; i < 2; i++) await Click(PhaseEditor.TallerSlot);
            Require((editor.RoomColumns, editor.RoomRows) == (80, 38), $"The size arrows made {editor.RoomColumns}x{editor.RoomRows}.");
            Capture(editor, "02-resized");

            // Holding the movement keys carries the cursor past the window's edges, and the window follows.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow)); await Task.Delay(2600);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
            Require(editor.Scroll.x > 0 && editor.Cursor.x >= editor.Scroll.x && editor.Cursor.x < editor.Scroll.x + PhaseEditor.Columns,
                $"The cursor {editor.Cursor} left the window at scroll {editor.Scroll}.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.DownArrow)); await Task.Delay(1500);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
            Require(editor.Scroll.y > 0 && editor.Cursor.y < editor.Scroll.y + PhaseEditor.Rows, $"The cursor {editor.Cursor} left the window at scroll {editor.Scroll}.");
            Capture(editor, "03-keyboard-scroll");

            // The wheel, Ctrl+wheel and a middle-button drag bring the view to the top-right corner, then back left by dragging.
            for (int i = 0; i < 10; i++) await Point(mouse, view, At(editor.Scroll.x + 5, editor.Scroll.y + 5), new Vector2(0, 1));
            Require(editor.Scroll.y == 0, $"The wheel left the view at row {editor.Scroll.y}.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl)); await Frames(2);
            for (int i = 0; i < 40 && editor.Scroll.x < 40; i++) await Point(mouse, view, At(editor.Scroll.x + 5, 5), new Vector2(0, -1));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
            Require(editor.Scroll == new Vector2Int(40, 0), $"Ctrl+wheel left the view at {editor.Scroll}.");
            await Point(mouse, view, At(60, 10), default, MouseButton.Middle); await Point(mouse, view, At(60, 10) + new Vector2Int(48, 0), default, MouseButton.Middle);
            await Point(mouse, view, At(60, 10) + new Vector2Int(48, 0));
            Require(editor.Scroll == new Vector2Int(34, 0), $"Dragging the room right did not reveal the cells to the left (scroll {editor.Scroll}).");
            for (int i = 0; i < 6; i++) await Point(mouse, view, At(50, 10), new Vector2(1, 0));
            Require(editor.Scroll.x == 40, "A sideways wheel did not scroll back to the right edge.");
            Capture(editor, "04-top-right");

            // Paint a floor across columns 40–79 of the bottom row the player stands on, then a platform's worth of terrain high up.
            await Point(mouse, view, At(40, 21), default, MouseButton.Left);
            for (int x = 41; x < 80; x++) await Point(mouse, view, At(x, 21), default, MouseButton.Left);
            await Point(mouse, view, At(79, 21));
            var floor = editor.Room().blocks.Count(b => b.y == 168);
            Require(floor == 80, $"The floor spans {floor} cells, not 80.");
            Capture(editor, "05-floor");

            // Export carries the size; a bad file is refused; the exported file restores the whole room.
            string draft = PhaseCustomMap.ToJson(editor.Room());
            Require(editor.ExportDraft() && GUIUtility.systemCopyBuffer == draft && draft.Contains("\"width\": 640") && draft.Contains("\"height\": 304"), "Export did not copy a 640x304 room.");
            editor.ImportDraft("{"); Require(PhaseCustomMap.ToJson(editor.Room()) == draft, "An invalid file changed the draft.");
            editor.ImportDraft("{\"name\":\"x\",\"blocks\":[{\"x\":0,\"y\":168,\"w\":320,\"h\":8,\"phaseMask\":2}],\"spawnX\":40,\"spawnY\":152}");
            Require((editor.RoomColumns, editor.RoomRows) == (40, 22), "An old one-screen file did not shrink the room.");
            await Click(PhaseEditor.ImportSlot);
            Require((editor.RoomColumns, editor.RoomRows, PhaseCustomMap.ToJson(editor.Room())) == (80, 38, draft), "The import slot did not restore the large room from the clipboard.");
            Capture(editor, "06-reimported");

            // Playtest: the whole room runs with the scrolling camera.
            await Click(PhaseEditor.PlaySlot);
            var playtest = editor.Playtest; Require(playtest != null, "The play slot did not start the playtest.");
            Require(playtest.Controller.Width == 640 && playtest.Controller.Height == 304, $"The playtest room is {playtest.Controller.Width}x{playtest.Controller.Height}.");
            Vector2Int Camera() { var at = playtest.pixelCamera.transform.position; return new Vector2Int(Mathf.RoundToInt(at.x - playtest.Frame.width / 2f), Mathf.RoundToInt(-at.y - playtest.Frame.height / 2f)); }
            var size = new Vector2Int(playtest.Frame.width, playtest.Frame.height);
            File.WriteAllBytes(Reports + "07-playtest-start.png", playtest.Frame.EncodeToPNG());
            int deaths = playtest.Controller.deaths; var start = Camera(); var seen = start;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            double deadline = Time.realtimeSinceStartupAsDouble + 25;
            while (playtest.Controller.player.x < 480 && Time.realtimeSinceStartupAsDouble < deadline)
            {
                await Frames(1); var now = Camera(); seen = new Vector2Int(Math.Max(seen.x, now.x), Math.Max(seen.y, now.y));
                Require(now.x >= 0 && now.x <= 640 - size.x && now.y >= 0 && now.y <= Math.Max(0, 304 - size.y), $"The camera left the room at {now}.");
                if (playtest.Controller.player.x > 400 && now.x > 100) File.WriteAllBytes(Reports + "08-playtest-scrolled.png", playtest.Frame.EncodeToPNG());
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
            Require(playtest.Controller.player.x >= 480 && playtest.Controller.deaths == deaths, $"Wick reached x={playtest.Controller.player.x} with {playtest.Controller.deaths - deaths} deaths.");
            Require(seen.x > start.x + 100, $"The camera followed only from {start} to {seen}.");
            File.WriteAllBytes(Reports + "09-playtest-end.png", playtest.Frame.EncodeToPNG());
            string moved = $"playerX={playtest.Controller.player.x:F0}, camera {start}→{Camera()}, view {size.x}x{size.y}";

            await Tap(keyboard, Key.Escape);
            Require(editor.Playtest == null && texture.width == view.x && texture.height == view.y, "Escape did not return to the editor view.");
            await Tap(keyboard, Key.Escape);
            await Scene(PhaseTitle.Scene);
            string report = $"passed: title→editor ({view.x}x{view.y} view for {Screen.width}x{Screen.height}), panel arrows grew the room to 80x38, cursor/wheel/Ctrl+wheel/middle-drag/sideways-wheel scrolling, " +
                "a floor painted across the scrolled columns, export carrying width/height, bad file refused, old file shrinking the room, re-import of the large room, playtest " + moved;
            File.WriteAllText(Reports + "live-input.txt", report); return report;
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            PhaseSandbox.ApplyControls(bindings);
            if (saved == null) PlayerPrefs.DeleteKey(PhaseEditor.DraftKey); else PlayerPrefs.SetString(PhaseEditor.DraftKey, saved);
            PlayerPrefs.Save(); GUIUtility.systemCopyBuffer = clipboard;
            InputSystem.settings.backgroundBehavior = focus; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background;
        }
    }
}
