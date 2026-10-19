using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Keyboard route: title → editor → paint → playtest → back → title → level 1. Restores the developer's draft.
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
        string draft = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null;
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

            await Tap(keyboard, Key.RightArrow); await Tap(keyboard, Key.Space);
            await Scene(PhaseEditor.Scene);
            var editor = UnityEngine.Object.FindAnyObjectByType<PhaseEditor>();
            await Tap(keyboard, Key.Digit3);
            for (int i = 0; i < 3; i++) await Tap(keyboard, Key.UpArrow);
            await Tap(keyboard, Key.Z);
            await Tap(keyboard, Key.RightArrow); await Tap(keyboard, Key.Z);
            Require(editor.Room().blocks.Count(b => b.phaseMask == 2) == 2, "Keyboard did not paint two blue cells.");
            Capture(editor, "editor");

            await Tap(keyboard, Key.Enter);
            var playtest = editor.Playtest; Require(playtest != null, "Enter did not start the playtest.");
            int frame = Time.frameCount; double time = playtest.Controller.time, x = playtest.Controller.player.x;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow)); await Task.Delay(300);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
            Require(Time.frameCount > frame && playtest.Controller.time > time && playtest.Controller.player.x > x, "Playtest did not advance with keyboard input.");
            File.WriteAllBytes(Reports + "playtest.png", playtest.Frame.EncodeToPNG());
            string moved = $"frames={Time.frameCount - frame}, controllerSeconds={playtest.Controller.time - time:F3}, dx={playtest.Controller.player.x - x:F0}";

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
            string report = "passed: title→editor, keyboard paint, playtest " + moved + ", escape back, title→level1";
            File.WriteAllText(Reports + "live-input.txt", report); return report;
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (draft == null) PlayerPrefs.DeleteKey(PhaseEditor.DraftKey); else PlayerPrefs.SetString(PhaseEditor.DraftKey, draft);
            PlayerPrefs.Save();
            InputSystem.settings.backgroundBehavior = focus; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background;
        }
    }
}
