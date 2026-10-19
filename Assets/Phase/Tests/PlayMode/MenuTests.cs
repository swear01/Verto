using System.Collections;
using System.Linq;
using NUnit.Framework;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class MenuTests : InputTestFixture
{
    Keyboard keyboard;
    IEnumerator Tap(params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
    }
    static IEnumerator WaitFor(string scene)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 5;
        while (SceneManager.GetActiveScene().name != scene && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(scene)); yield return null;
    }
    IEnumerator OpenTitle()
    {
        yield return SceneManager.LoadSceneAsync(PhaseTitle.Scene);
        keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
    }
    [UnityTest]
    public IEnumerator TitleIsFirstSceneAndJumpStartsLevelOne()
    {
        Assert.That(SceneUtility.GetScenePathByBuildIndex(0), Is.EqualTo("Assets/Scenes/PhaseTitle.unity"));
        yield return OpenTitle();
        Assert.That(Object.FindAnyObjectByType<PhaseTitle>().Choice, Is.Zero);
        yield return Tap(Key.Z);
        yield return WaitFor("PhaseDemo1");
        LogAssert.NoUnexpectedReceived();
    }
    [UnityTest]
    public IEnumerator EditorPaintsPlaytestsAndKeepsDraft()
    {
        // Restore any real draft the developer has in this Editor's PlayerPrefs.
        string saved = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null, clipboard = GUIUtility.systemCopyBuffer;
        PlayerPrefs.DeleteKey(PhaseEditor.DraftKey);
        try
        {
            yield return OpenTitle();
            yield return Tap(Key.RightArrow); yield return Tap(Key.RightArrow);
            Assert.That(Object.FindAnyObjectByType<PhaseTitle>().Choice, Is.EqualTo(2));
            yield return Tap(Key.Z);
            yield return WaitFor(PhaseEditor.Scene);
            var editor = Object.FindAnyObjectByType<PhaseEditor>();
            Assert.That(editor.Room().blocks.Length, Is.EqualTo(PhaseEditor.Columns), "new draft starts with a floor");
            yield return Tap(Key.Digit2);
            yield return Tap(Key.RightArrow);
            Assert.That((editor.Mask, editor.Cursor), Is.EqualTo((1, new Vector2Int(9, 18))));
            yield return Tap(Key.Z);
            yield return Tap(Key.RightArrow);
            yield return Tap(Key.X);
            Assert.That(editor.Tool, Is.EqualTo(2));
            yield return Tap(Key.Z);
            for (int i = 0; i < 4; i++) yield return Tap(Key.X);
            Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.LaserTool));
            yield return Tap(Key.RightArrow); yield return Tap(Key.Z);
            for (int i = 0; i < 5; i++) yield return Tap(Key.X);
            Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.CrystalTool + 1));
            yield return Tap(Key.Digit5); yield return Tap(Key.RightArrow); yield return Tap(Key.Z);
            var room = editor.Room();
            Assert.That(room.blocks.Any(b => b.x == 72 && b.y == 144 && b.phaseMask == 1), Is.True, "jump paints red terrain");
            var spike = room.spikes.Single();
            Assert.That((spike.x, spike.y, spike.direction, spike.phaseMask), Is.EqualTo((80.0, 144.0, SpikeDirection.Up, 1)));
            var laser = room.lasers.Single();
            Assert.That((laser.x, laser.y, laser.direction, laser.phaseMask), Is.EqualTo((88.0, 144.0, LaserDirection.Up, 1)));
            var crystal = room.crystals.Single();
            Assert.That((crystal.x, crystal.y, crystal.kind, crystal.phaseMask), Is.EqualTo((96.0, 144.0, CrystalKind.Dash, 5)));

            // Outside Web the import/export icons use the clipboard.
            Assert.That(editor.ExportDraft(), Is.True);
            string exported = GUIUtility.systemCopyBuffer, draft = PhaseCustomMap.ToJson(room);
            Assert.That(exported, Is.EqualTo(draft));
            editor.ImportDraft("{");
            Assert.That(PhaseCustomMap.ToJson(editor.Room()), Is.EqualTo(draft), "an invalid file leaves the draft unchanged");
            editor.ImportDraft("{\"name\":\"x\",\"blocks\":[{\"x\":0,\"y\":168,\"w\":320,\"h\":8,\"phaseMask\":2}],\"spawnX\":40,\"spawnY\":152}");
            Assert.That((editor.Room().blocks.Length, editor.Room().blocks[0].phaseMask, editor.SpawnCell), Is.EqualTo((PhaseEditor.Columns, 2, new Vector2Int(5, 19))), "wide blocks import as cells");
            editor.ImportDraft(exported);
            Assert.That(PhaseCustomMap.ToJson(editor.Room()), Is.EqualTo(draft), "export then import restores the draft");

            yield return Tap(Key.Enter);
            var sandbox = editor.Playtest;
            Assert.That(sandbox, Is.Not.Null);
            Assert.That(sandbox.gameObject.name, Is.EqualTo("Playtest"));
            Assert.That(GameObject.Find("PHASE"), Is.SameAs(editor.gameObject), "the Web bridge still reaches the editor");
            Assert.That((sandbox.Controller.blocks.Count, sandbox.Controller.spikes.Count, sandbox.Controller.lasers.Count, sandbox.Controller.crystals.Count),
                Is.EqualTo((PhaseEditor.Columns + 1, 1, 1, 1)));
            double start = sandbox.Controller.player.x, time = sandbox.Controller.time;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow)); yield return new WaitForSeconds(.2f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(sandbox.Controller.time, Is.GreaterThan(time));
            Assert.That(sandbox.Controller.player.x, Is.GreaterThan(start));

            yield return Tap(Key.Escape);
            Assert.That(editor.Playtest, Is.Null);
            Assert.That(sandbox == null, Is.True, "playtest room is destroyed");
            yield return Tap(Key.Escape);
            yield return WaitFor(PhaseTitle.Scene);
            yield return Tap(Key.RightArrow); yield return Tap(Key.RightArrow);
            yield return Tap(Key.Z);
            yield return WaitFor(PhaseEditor.Scene);
            room = Object.FindAnyObjectByType<PhaseEditor>().Room();
            Assert.That(room.blocks.Any(b => b.x == 72 && b.y == 144 && b.phaseMask == 1), Is.True, "draft survives leaving the editor");
            Assert.That((room.spikes.Length, room.lasers.Length, room.crystals.Length), Is.EqualTo((1, 1, 1)));
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (saved == null) PlayerPrefs.DeleteKey(PhaseEditor.DraftKey); else PlayerPrefs.SetString(PhaseEditor.DraftKey, saved);
            PlayerPrefs.Save(); GUIUtility.systemCopyBuffer = clipboard;
        }
    }

    static PhaseSandbox Sandbox() => Object.FindAnyObjectByType<PhaseSandbox>();
    [UnityTest]
    public IEnumerator PauseFreezesPlayAndKeySettingsRebindAndPersist()
    {
        string saved = PlayerPrefs.HasKey(PhaseSandbox.ControlsKey) ? PlayerPrefs.GetString(PhaseSandbox.ControlsKey) : null;
        try
        {
            yield return SceneManager.LoadSceneAsync("PhaseDemo1");
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            var sandbox = Sandbox();
            yield return Tap(Key.Escape);
            Assert.That(sandbox.Menu, Is.Not.Null, "Escape pauses");
            double time = sandbox.Controller.time;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.C)); yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(sandbox.Controller.time, Is.EqualTo(time), "pause freezes play");
            yield return Tap(Key.RightArrow); yield return Tap(Key.RightArrow);
            Assert.That(sandbox.Menu.Choice, Is.EqualTo(2));
            yield return Tap(Key.Enter);
            var keys = sandbox.Keys;
            Assert.That(keys, Is.Not.Null, "the keyboard icon opens key settings");
            for (int i = 0; i < 4; i++) yield return Tap(Key.DownArrow);
            yield return Tap(Key.Enter);
            Assert.That(keys.Capturing, Is.True);
            yield return Tap(Key.J);
            Assert.That((keys.Capturing, PhaseSandbox.Slots[8]), Is.EqualTo((false, "J")), "the next key binds the jump slot");
            Assert.That(PlayerPrefs.GetString(PhaseSandbox.ControlsKey).Split(',')[8], Is.EqualTo("J"), "bindings persist");
            yield return Tap(Key.RightArrow); yield return Tap(Key.Enter); yield return Tap(Key.J);
            Assert.That((keys.Flashing, PhaseSandbox.Slots[9]), Is.EqualTo((true, "Space")), "a duplicate key flashes and is not applied");
            yield return Tap(Key.Enter); yield return Tap(Key.F1);
            Assert.That((keys.Flashing, PhaseSandbox.Slots[9]), Is.EqualTo((true, "Space")), "an unsupported key is not applied");
            yield return Tap(Key.Enter); yield return Tap(Key.Escape);
            Assert.That((keys.Capturing, sandbox.Keys, PhaseSandbox.Slots[9]), Is.EqualTo((false, keys, "Space")), "Escape cancels a capture");
            yield return Tap(Key.Backspace);
            Assert.That(PhaseSandbox.Slots[9], Is.Empty, "Backspace clears a slot");
            yield return new WaitForSeconds(.5f);
            yield return Tap(Key.LeftArrow); yield return Tap(Key.Backspace);
            Assert.That((keys.Flashing, PhaseSandbox.Slots[8]), Is.EqualTo((true, "J")), "the last key of an action stays");
            yield return Tap(Key.Escape);
            Assert.That((sandbox.Keys, sandbox.Menu != null), Is.EqualTo(((PhaseKeys)null, true)), "Escape returns to the pause menu");
            yield return Tap(Key.Escape);
            Assert.That(sandbox.Menu, Is.Null, "Escape resumes");
            sandbox.Controller.Reset();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J)); yield return new WaitForSeconds(.05f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(sandbox.Controller.jumps, Is.EqualTo(1), "the new key jumps");

            yield return Tap(Key.Escape); yield return Tap(Key.RightArrow); yield return Tap(Key.RightArrow); yield return Tap(Key.Enter);
            for (int i = 0; i < PhaseKeys.RestoreRow; i++) yield return Tap(Key.DownArrow);
            yield return Tap(Key.Enter);
            Assert.That(PhaseSandbox.Slots, Is.EqualTo(PhaseSandbox.DefaultSlots), "restore defaults");
            Assert.That(PlayerPrefs.GetString(PhaseSandbox.ControlsKey), Is.EqualTo(string.Join(",", PhaseSandbox.DefaultSlots)));
            yield return Tap(Key.Escape);
            yield return Tap(Key.RightArrow);
            Assert.That(sandbox.Menu.Choice, Is.EqualTo(3));
            yield return Tap(Key.Z);
            yield return WaitFor(PhaseTitle.Scene);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            PhaseSandbox.ApplyControls(PhaseSandbox.DefaultSlots);
            if (saved == null) PlayerPrefs.DeleteKey(PhaseSandbox.ControlsKey); else PlayerPrefs.SetString(PhaseSandbox.ControlsKey, saved);
            PlayerPrefs.Save(); PhaseSandbox.LoadControls();
        }
    }
    [UnityTest]
    public IEnumerator TitlePauseAndVictoryOpenLevelSelect()
    {
        yield return OpenTitle();
        var title = Object.FindAnyObjectByType<PhaseTitle>();
        for (int i = 0; i < 3; i++) yield return Tap(Key.RightArrow);
        yield return Tap(Key.Enter);
        Assert.That((title.Choice, title.Keys != null), Is.EqualTo((3, true)), "title keyboard icon opens key settings");
        yield return Tap(Key.Escape);
        Assert.That(title.Keys, Is.Null);
        yield return Tap(Key.LeftArrow); yield return Tap(Key.LeftArrow);
        yield return Tap(Key.Z);
        Assert.That(title.LevelSelect, Is.Not.Null, "title grid icon opens level select");
        yield return Tap(Key.Escape);
        Assert.That(title.LevelSelect, Is.Null);
        yield return Tap(Key.Enter);
        yield return Tap(Key.RightArrow); yield return Tap(Key.DownArrow);
        Assert.That(title.LevelSelect.Choice, Is.EqualTo(5));
        yield return Tap(Key.Enter);
        yield return WaitFor("PhaseDemo6");

        yield return Tap(Key.Escape); yield return Tap(Key.RightArrow); yield return Tap(Key.Enter);
        var levels = Sandbox().LevelSelect;
        Assert.That(levels.Choice, Is.EqualTo(5), "pause level select starts at the current level");
        yield return Tap(Key.DownArrow); yield return Tap(Key.DownArrow); yield return Tap(Key.DownArrow); yield return Tap(Key.RightArrow);
        Assert.That(levels.Choice, Is.EqualTo(10));
        yield return Tap(Key.Z);
        yield return WaitFor(PhaseSandbox.Levels[10]);

        yield return SceneManager.LoadSceneAsync("PhaseDemo12"); yield return null;
        var sandbox = Sandbox();
        typeof(PhaseSandbox).GetMethod("Win", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(sandbox, null);
        yield return null;
        Assert.That((sandbox.Won, sandbox.VictoryMenu.Choice), Is.EqualTo((true, 0)));
        yield return Tap(Key.RightArrow); yield return Tap(Key.Enter);
        Assert.That(sandbox.LevelSelect, Is.Not.Null, "victory grid icon opens level select");
        yield return Tap(Key.Escape);
        Assert.That((sandbox.LevelSelect, sandbox.Won), Is.EqualTo(((PhaseLevels)null, true)), "Escape returns to the victory screen");
        yield return Tap(Key.LeftArrow); yield return Tap(Key.Enter);
        yield return WaitFor("PhaseDemo1");
        LogAssert.NoUnexpectedReceived();
    }
}
