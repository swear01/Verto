using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Keyboard route: title → editor → paint terrain, laser, crystal, place a platform and a gear; then a virtual mouse sweeps
// the grid under the fixed panel, picks a color and tool, paints and erases, and its play slot starts the playtest
// → export/import → back → title → level 1. Restores the developer's draft and clipboard.
public static class MenuLiveInput
{
    const string Reports = "TestResults/Menu/";
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
    // Points at a frame pixel's center, inverting PhaseScreen.FramePoint, with the given buttons held.
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
            keyboard = InputSystem.AddDevice<Keyboard>("PhaseMenuVerification");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
            Capture(UnityEngine.Object.FindAnyObjectByType<PhaseTitle>(), "title");

            await Tap(keyboard, Key.RightArrow); await Tap(keyboard, Key.RightArrow); await Tap(keyboard, Key.Space);
            await Scene(PhaseEditor.Scene);
            var editor = UnityEngine.Object.FindAnyObjectByType<PhaseEditor>();
            await Tap(keyboard, Key.Digit3);
            for (int i = 0; i < 3; i++) await Tap(keyboard, Key.UpArrow);
            await Tap(keyboard, Key.Z);
            await Tap(keyboard, Key.RightArrow); await Tap(keyboard, Key.Z);
            Require(editor.Room().blocks.Count(b => b.phaseMask == 2) == 2, "Keyboard did not paint two blue cells.");
            for (int i = 0; i < 6; i++) await Tap(keyboard, Key.X);
            Require(editor.Tool == PhaseEditor.LaserTool + 1, "Dash did not cycle to the down laser.");
            await Tap(keyboard, Key.Digit2); await Tap(keyboard, Key.UpArrow); await Tap(keyboard, Key.Z);
            for (int i = 0; i < 3; i++) await Tap(keyboard, Key.X);
            Require(editor.Tool == PhaseEditor.CrystalTool, "Dash did not cycle to the jump crystal.");
            await Tap(keyboard, Key.Digit1); await Tap(keyboard, Key.RightArrow); await Tap(keyboard, Key.Z);
            var room = editor.Room();
            Require(room.lasers.Length == 1 && room.lasers[0].phaseMask == 1 && room.lasers[0].direction == LaserDirection.Down, "Keyboard did not paint a red down laser.");
            Require(room.crystals.Length == 1 && room.crystals[0].phaseMask == 7 && room.crystals[0].kind == CrystalKind.Jump, "Keyboard did not paint a white jump crystal.");
            await Tap(keyboard, Key.X); await Tap(keyboard, Key.X);
            Require(editor.Tool == PhaseEditor.ReturnPlatform, "Dash did not cycle to the returning platform.");
            for (int i = 0; i < 4; i++) await Tap(keyboard, Key.UpArrow);
            await Tap(keyboard, Key.Z);
            for (int i = 0; i < 4; i++) await Tap(keyboard, Key.RightArrow);
            await Tap(keyboard, Key.Z);
            room = editor.Room();
            Require(room.platforms.Length == 1 && room.platforms[0].returns && room.platforms[0].nodeX.SequenceEqual(new[] { 80.0, 112.0 }) && room.platforms[0].nodeY.SequenceEqual(new[] { 80.0, 80.0 }),
                "Keyboard did not place a returning platform from (80,80) to (112,80).");
            await Tap(keyboard, Key.X); await Tap(keyboard, Key.X);
            Require(editor.Tool == PhaseEditor.GearTool, "Dash did not cycle to the gear.");
            for (int i = 0; i < 6; i++) await Tap(keyboard, Key.RightArrow);
            await Tap(keyboard, Key.Z);
            for (int i = 0; i < 5; i++) await Tap(keyboard, Key.RightArrow);
            for (int i = 0; i < 6; i++) await Tap(keyboard, Key.DownArrow);
            Require(editor.Anchor == new Vector2Int(20, 10), "Jump did not place the gear wall.");
            Capture(editor, "editor-gear-end");
            await Tap(keyboard, Key.Z);
            room = editor.Room();
            Require(room.gears.Length == 1 && room.gears[0].wall.x == 160 && room.gears[0].top == 56 && room.gears[0].platform.x == 176 && room.gears[0].platform.y == 128 && room.gears[0].endX == 200,
                "Keyboard did not place the gear at wall (160,80) with a bridge from 176 to 200 on row 128.");
            Capture(editor, "editor");

            mouse = InputSystem.AddDevice<Mouse>("PhaseMenuVerificationMouse");
            var view = PhaseEditor.ViewFor(Screen.width, Screen.height);
            var texture = editor.GetComponentInChildren<SpriteRenderer>().sprite.texture;
            Require(texture.width == view.x && texture.height == view.y, $"The editor did not draw its {view.x}x{view.y} view.");
            var panel = PhaseEditor.PanelIn(view);
            Color[] Panel() => texture.GetPixels(panel.x, view.y - panel.yMax, panel.width, panel.height);
            Vector2Int Cell(int cx, int cy) => new Vector2Int(cx * 8 + 4, cy * 8 + 4 - (180 - view.y) / 2);
            Vector2Int Slot(int slot) { var r = PhaseEditor.SlotIn(view, slot); return new Vector2Int(r.x + r.width / 2, r.y + r.height / 2); }
            await Point(mouse, view, Cell(30, 0)); var still = Panel();
            foreach (var cell in new[] { new Vector2Int(39, 0), new Vector2Int(30, 21), new Vector2Int(0, 21), new Vector2Int(30, 0) })
            {
                await Point(mouse, view, Cell(cell.x, cell.y));
                Require(editor.Cursor == cell && editor.Hover == null && Panel().SequenceEqual(still), $"The cursor missed {cell} or the panel changed.");
            }
            int yellow = PhaseEditor.MaskSlot + 4;
            await Point(mouse, view, Slot(yellow));
            Require(editor.Hover == yellow && !Panel().SequenceEqual(still), "Hovering the yellow slot did not highlight it.");
            Capture(editor, "editor-hover");
            await Point(mouse, view, Slot(yellow), MouseButton.Left); await Point(mouse, view, Slot(yellow));
            await Point(mouse, view, Slot(0), MouseButton.Left); await Point(mouse, view, Cell(30, 5), MouseButton.Left); await Point(mouse, view, Cell(30, 5));
            Require(editor.Tool == PhaseEditor.Terrain && editor.Mask == 5 && PhaseCustomMap.ToJson(editor.Room()) == PhaseCustomMap.ToJson(room),
                "Panel clicks did not pick yellow terrain, or a drag from the panel painted.");
            await Point(mouse, view, Cell(30, 5), MouseButton.Left); await Point(mouse, view, Cell(30, 5));
            Require(editor.Room().blocks.Any(b => b.x == 240 && b.y == 40 && b.phaseMask == 5), "A click on the grid did not paint yellow terrain.");
            Capture(editor, "editor-mouse");
            await Point(mouse, view, Cell(30, 5), MouseButton.Right); await Point(mouse, view, Cell(30, 5));
            Require(PhaseCustomMap.ToJson(editor.Room()) == PhaseCustomMap.ToJson(room), "Right-click did not erase the painted cell.");
            string draft = PhaseCustomMap.ToJson(room);
            Require(editor.ExportDraft() && GUIUtility.systemCopyBuffer == draft, "Export did not copy the draft.");
            editor.ImportDraft("{"); Require(PhaseCustomMap.ToJson(editor.Room()) == draft, "Invalid import changed the draft.");
            editor.ImportDraft(draft); Require(PhaseCustomMap.ToJson(editor.Room()) == draft, "Import did not restore the draft.");

            await Point(mouse, view, Slot(PhaseEditor.PlaySlot), MouseButton.Left); await Point(mouse, view, Slot(PhaseEditor.PlaySlot));
            var playtest = editor.Playtest; Require(playtest != null, "The play slot did not start the playtest.");
            Require(playtest.Controller.lasers.Count == 1 && playtest.Controller.crystals.Count == 1 && playtest.Controller.movers.Length == 1 && playtest.Controller.gears.Length == 1,
                "Playtest is missing the laser, crystal, platform or gear.");
            int frame = Time.frameCount; double time = playtest.Controller.time, x = playtest.Controller.player.x;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow)); await Task.Delay(300);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
            Require(Time.frameCount > frame && playtest.Controller.time > time && playtest.Controller.player.x > x, "Playtest did not advance with keyboard input.");
            await Tap(keyboard, Key.X);
            for (int i = 0; i < 60 && playtest.Controller.movers[0].x <= 80; i++) await Frames(1);
            Require(playtest.Controller.movers[0].x > 80, $"Dash did not move the playtest platform (dashes={playtest.Controller.dashes}).");
            File.WriteAllBytes(Reports + "playtest.png", playtest.Frame.EncodeToPNG());
            string moved = $"frames={Time.frameCount - frame}, controllerSeconds={playtest.Controller.time - time:F3}, dx={playtest.Controller.player.x - x:F0}, platformX={playtest.Controller.movers[0].x:F0}";

            await Tap(keyboard, Key.Escape);
            Require(editor.Playtest == null && texture.width == view.x && texture.height == view.y, "Escape did not return to the editor view.");
            await Tap(keyboard, Key.Escape);
            await Scene(PhaseTitle.Scene);
            await Tap(keyboard, Key.Z);
            await Scene("PhaseDemo1");
            var level = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
            time = level.Controller.time; await Frames(10);
            Require(level.Controller.time > time, "Level1 did not start.");
            File.WriteAllBytes(Reports + "level1.png", level.Frame.EncodeToPNG());
            string report = $"passed: title→editor ({view.x}x{view.y} view for {Screen.width}x{Screen.height}), keyboard terrain/laser/crystal paint, two-step platform/gear placement, " +
                "fixed panel under a top/bottom-row sweep, mouse hover/color/tool/paint/erase, panel drag paints nothing, export/import, play slot → playtest " + moved + ", escape back, title→level1";
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
