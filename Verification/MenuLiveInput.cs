using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Keyboard route: title → editor → paint terrain, laser, crystal, draw a diagonal platform line and a gear → export/import → playtest → back → title → level 1.
// Restores the developer's draft and clipboard.
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
        Keyboard keyboard = null;
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
            Require(editor.Tool == PhaseEditor.PlatformTool, "Dash did not cycle to the platform.");
            for (int i = 0; i < 4; i++) await Tap(keyboard, Key.UpArrow);
            await Tap(keyboard, Key.Z);
            for (int i = 0; i < 4; i++) await Tap(keyboard, Key.RightArrow);
            for (int i = 0; i < 2; i++) await Tap(keyboard, Key.UpArrow);
            await Tap(keyboard, Key.Z);
            room = editor.Room();
            Require(room.platforms.Length == 1 && !room.platforms[0].returns && room.platforms[0].w == 24 && room.platforms[0].h == 8 &&
                room.platforms[0].nodeX.SequenceEqual(new[] { 80.0, 112.0 }) && room.platforms[0].nodeY.SequenceEqual(new[] { 80.0, 64.0 }),
                "Keyboard did not draw a diagonal platform from (80,80) to (112,64).");
            Capture(editor, "editor-platform");
            for (int i = 0; i < 2; i++) await Tap(keyboard, Key.DownArrow);
            await Tap(keyboard, Key.X);
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
            string draft = PhaseCustomMap.ToJson(room);
            Require(editor.ExportDraft() && GUIUtility.systemCopyBuffer == draft, "Export did not copy the draft.");
            editor.ImportDraft("{"); Require(PhaseCustomMap.ToJson(editor.Room()) == draft, "Invalid import changed the draft.");
            editor.ImportDraft(draft); Require(PhaseCustomMap.ToJson(editor.Room()) == draft, "Import did not restore the draft.");

            await Tap(keyboard, Key.Enter);
            var playtest = editor.Playtest; Require(playtest != null, "Enter did not start the playtest.");
            Require(playtest.Controller.lasers.Count == 1 && playtest.Controller.crystals.Count == 1 && playtest.Controller.movers.Length == 1 && playtest.Controller.gears.Length == 1,
                "Playtest is missing the laser, crystal, platform or gear.");
            int frame = Time.frameCount; double time = playtest.Controller.time, x = playtest.Controller.player.x;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow)); await Task.Delay(300);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
            Require(Time.frameCount > frame && playtest.Controller.time > time && playtest.Controller.player.x > x, "Playtest did not advance with keyboard input.");
            await Tap(keyboard, Key.X);
            for (int i = 0; i < 60 && (playtest.Controller.movers[0].x <= 80 || playtest.Controller.movers[0].y >= 80); i++) await Frames(1);
            Require(playtest.Controller.movers[0].x > 80 && playtest.Controller.movers[0].y < 80, $"Dash did not move the playtest platform along the diagonal (dashes={playtest.Controller.dashes}, x={playtest.Controller.movers[0].x}, y={playtest.Controller.movers[0].y}).");
            File.WriteAllBytes(Reports + "playtest.png", playtest.Frame.EncodeToPNG());
            string moved = $"frames={Time.frameCount - frame}, controllerSeconds={playtest.Controller.time - time:F3}, dx={playtest.Controller.player.x - x:F0}, platformX={playtest.Controller.movers[0].x:F0}";

            await Tap(keyboard, Key.Escape);
            Require(editor.Playtest == null, "Escape did not return to the editor.");
            await Tap(keyboard, Key.Escape);
            await Scene(PhaseTitle.Scene);
            await Tap(keyboard, Key.Z);
            await Scene("PhaseDemo1");
            var level = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
            time = level.Controller.time; await Frames(10);
            Require(level.Controller.time > time, "Level1 did not start.");
            File.WriteAllBytes(Reports + "level1.png", level.Frame.EncodeToPNG());
            string report = "passed: title→editor, keyboard terrain/laser/crystal paint, diagonal platform line and gear, export/import, playtest " + moved + ", escape back, title→level1";
            File.WriteAllText(Reports + "live-input.txt", report); return report;
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (saved == null) PlayerPrefs.DeleteKey(PhaseEditor.DraftKey); else PlayerPrefs.SetString(PhaseEditor.DraftKey, saved);
            PlayerPrefs.Save(); GUIUtility.systemCopyBuffer = clipboard;
            InputSystem.settings.backgroundBehavior = focus; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background;
        }
    }
}
