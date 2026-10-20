using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Keyboard route: title settings → bind J to jump → title level select → level1 jumps with J → pause freezes play →
// pause level select → level2 → restore default keys → fullscreen on and off → back to title. Restores the developer's saved bindings.
public static class KeysLiveInput
{
    const string Reports = "TestResults/Keys/";
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
    static async Task Repeat(Keyboard keyboard, Key key, int count) { for (int i = 0; i < count; i++) await Tap(keyboard, key); }
    static async Task Scene(string name)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 15;
        while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartupAsDouble < deadline) await Task.Delay(16);
        Require(SceneManager.GetActiveScene().name == name, "Did not reach " + name); await Frames(3);
    }
    static void Capture(MonoBehaviour screen, string name) =>
        File.WriteAllBytes(Reports + name + ".png", ((Texture2D)screen.GetComponentsInChildren<SpriteRenderer>().First(r => r.enabled).sprite.texture).EncodeToPNG());
    static PhaseSandbox Level() => UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
    public static async Task<string> Run()
    {
        Require(Application.isPlaying && SceneManager.GetActiveScene().name == PhaseTitle.Scene, "Open PhaseTitle in Play mode first.");
        bool background = Application.runInBackground;
        var focus = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        string saved = PlayerPrefs.HasKey(PhaseSandbox.ControlsKey) ? PlayerPrefs.GetString(PhaseSandbox.ControlsKey) : null;
        var slots = PhaseSandbox.Slots.ToArray();
        Keyboard keyboard = null;
        var (isFull, setFull) = (PhaseKeys.IsFullScreen, PhaseKeys.SetFullScreen);
        try
        {
            Directory.CreateDirectory(Reports);
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            PhaseSandbox.ApplyControls(PhaseSandbox.DefaultSlots);
            keyboard = InputSystem.AddDevice<Keyboard>("PhaseKeysVerification");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
            var title = UnityEngine.Object.FindAnyObjectByType<PhaseTitle>();
            Capture(title, "title");

            await Repeat(keyboard, Key.RightArrow, 3); await Tap(keyboard, Key.Enter);
            Require(title.Keys != null, "The gear did not open settings.");
            await Repeat(keyboard, Key.DownArrow, 4); await Tap(keyboard, Key.Enter); await Tap(keyboard, Key.J);
            Require(PhaseSandbox.Slots[8] == "J" && PlayerPrefs.GetString(PhaseSandbox.ControlsKey).Split(',')[8] == "J", "J was not bound and saved to jump.");
            await Tap(keyboard, Key.RightArrow); await Tap(keyboard, Key.Enter); await Tap(keyboard, Key.J);
            Require(title.Keys.Flashing && PhaseSandbox.Slots[9] == "Space", "A duplicate key was not rejected.");
            Capture(title, "keys-rejected");
            await Task.Delay(500); await Frames(2);
            Capture(title, "keys");
            await Tap(keyboard, Key.Escape);
            Require(title.Keys == null, "Escape did not close settings.");

            await Repeat(keyboard, Key.LeftArrow, 2); await Tap(keyboard, Key.Enter);
            Require(title.LevelSelect != null, "The grid icon did not open level select.");
            Capture(title, "levels");
            await Tap(keyboard, Key.Enter);
            await Scene("PhaseDemo1");
            var level = Level();
            await Frames(10);
            level.Controller.Reset();
            int frame = Time.frameCount; double time = level.Controller.time;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J)); await Task.Delay(100);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(2);
            Require(level.Controller.jumps == 1 && level.Controller.time > time, "The rebound J key did not jump.");
            string played = $"frames={Time.frameCount - frame}, controllerSeconds={level.Controller.time - time:F3}";

            await Tap(keyboard, Key.Escape);
            Require(level.Menu != null, "Escape did not pause.");
            time = level.Controller.time; await Frames(20);
            Require(level.Controller.time == time, "Pause did not freeze play.");
            Capture(level, "pause");
            await Tap(keyboard, Key.RightArrow); await Tap(keyboard, Key.Enter);
            Require(level.LevelSelect != null && level.LevelSelect.Choice == 0, "Pause level select did not open at level 1.");
            await Tap(keyboard, Key.RightArrow); await Tap(keyboard, Key.Enter);
            await Scene("PhaseDemo1.1");
            level = Level();

            await Tap(keyboard, Key.Escape); await Repeat(keyboard, Key.RightArrow, 2); await Tap(keyboard, Key.Enter);
            Require(level.Keys != null, "Pause gear did not open settings.");
            await Repeat(keyboard, Key.DownArrow, PhaseKeys.RestoreRow); await Tap(keyboard, Key.Enter);
            Require(PhaseSandbox.Slots.SequenceEqual(PhaseSandbox.DefaultSlots), "Restore did not reset the bindings.");
            Capture(level, "keys-restored");
            // The Editor ignores Screen.fullScreen; a stand-in records the switch's requests.
            bool full = false; PhaseKeys.IsFullScreen = () => full; PhaseKeys.SetFullScreen = on => full = on;
            await Tap(keyboard, Key.DownArrow); await Tap(keyboard, Key.Enter); await Frames(2);
            Require(level.Keys.Row == PhaseKeys.FullScreenRow && full, "Enter on the last row did not request fullscreen.");
            Capture(level, "settings-fullscreen");
            await Tap(keyboard, Key.Z);
            Require(!full, "Jump on the switch did not leave fullscreen.");
            await Tap(keyboard, Key.Escape); await Tap(keyboard, Key.RightArrow);
            Require(level.Menu.Choice == 3, "Title icon was not selected.");
            await Tap(keyboard, Key.Enter);
            await Scene(PhaseTitle.Scene);
            string report = "passed: title settings bind J + duplicate rejected, title level select→level1, J jumps " + played +
                ", Escape pause freezes play, pause level select→next level, pause settings restore defaults, fullscreen on/off, pause→title";
            File.WriteAllText(Reports + "live-input.txt", report); return report;
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            PhaseKeys.IsFullScreen = isFull; PhaseKeys.SetFullScreen = setFull;
            if (saved == null) PlayerPrefs.DeleteKey(PhaseSandbox.ControlsKey); else PlayerPrefs.SetString(PhaseSandbox.ControlsKey, saved);
            PlayerPrefs.Save(); PhaseSandbox.ApplyControls(slots);
            InputSystem.settings.backgroundBehavior = focus; InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = background;
        }
    }
}
