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
            yield return Tap(Key.RightArrow);
            Assert.That(Object.FindAnyObjectByType<PhaseTitle>().Choice, Is.EqualTo(1));
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
            yield return Tap(Key.RightArrow);
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
    IEnumerator Move(Key key, int count) { for (int i = 0; i < count; i++) yield return Tap(key); }
    [UnityTest]
    public IEnumerator EditorPlacesPlatformsAndGearsInTwoSteps()
    {
        string saved = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null, clipboard = GUIUtility.systemCopyBuffer;
        PlayerPrefs.DeleteKey(PhaseEditor.DraftKey);
        try
        {
            yield return OpenTitle();
            yield return Tap(Key.RightArrow); yield return Tap(Key.Z);
            yield return WaitFor(PhaseEditor.Scene);
            var editor = Object.FindAnyObjectByType<PhaseEditor>();
            yield return Move(Key.X, 11);
            Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.ReturnPlatform));
            yield return Move(Key.UpArrow, 4);
            yield return Tap(Key.Z);
            Assert.That((editor.Anchor, editor.Room().platforms.Length), Is.EqualTo(((Vector2Int?)new Vector2Int(8, 14), 0)), "jump places the body first");
            yield return Tap(Key.Escape);
            Assert.That((editor.Anchor, Object.FindAnyObjectByType<PhaseEditor>()), Is.EqualTo(((Vector2Int?)null, editor)), "escape cancels without leaving");
            yield return Tap(Key.Z); yield return Move(Key.RightArrow, 4); yield return Tap(Key.UpArrow); yield return Tap(Key.Z);
            var platform = editor.Room().platforms.Single();
            Assert.That((string.Join(",", platform.nodeX.Concat(platform.nodeY)), platform.w, platform.h, platform.returns, editor.Anchor),
                Is.EqualTo(("64,96,112,112", 24.0, 8.0, true, (Vector2Int?)null)), "a returning route keeps to one axis");

            yield return Tap(Key.X);
            Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.PlatformTool));
            yield return Move(Key.RightArrow, 8); yield return Move(Key.UpArrow, 3);
            yield return Tap(Key.Z); yield return Tap(Key.C);
            Assert.That((editor.Anchor, editor.Room().platforms.Length), Is.EqualTo(((Vector2Int?)null, 1)), "erase cancels a pending body");
            yield return Tap(Key.Z); yield return Move(Key.RightArrow, 4); yield return Move(Key.DownArrow, 2); yield return Tap(Key.Z);
            platform = editor.Room().platforms.Last();
            Assert.That((string.Join(",", platform.nodeX.Concat(platform.nodeY)), platform.returns), Is.EqualTo(("160,192,80,96", false)));

            yield return Tap(Key.X);
            Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.GearTool));
            yield return Move(Key.RightArrow, 4); yield return Move(Key.UpArrow, 2);
            yield return Tap(Key.Z); yield return Move(Key.RightArrow, 5); yield return Move(Key.DownArrow, 6); yield return Tap(Key.Z);
            var gear = editor.Room().gears.Single();
            Assert.That((gear.wall.x, gear.wall.y, gear.wall.w, gear.wall.h, gear.top, gear.platform.x, gear.platform.y, gear.platform.w, gear.platform.h, gear.endX),
                Is.EqualTo((224.0, 80.0, 8.0, 56.0, 56.0, 240.0, 128.0, 56.0, 8.0, 264.0)));

            yield return Move(Key.LeftArrow, 13); yield return Move(Key.UpArrow, 6);
            Assert.That(editor.Cursor, Is.EqualTo(new Vector2Int(20, 10)));
            yield return Move(Key.X, 3); yield return Tap(Key.Z);
            Assert.That((editor.Tool, editor.Room().blocks.Length), Is.EqualTo((PhaseEditor.Terrain, PhaseEditor.Columns)), "terrain never paints over a platform");
            yield return Tap(Key.C);
            Assert.That(editor.Room().platforms.Length, Is.EqualTo(1), "erase removes the platform under the cursor");

            Assert.That(editor.ExportDraft(), Is.True);
            string exported = GUIUtility.systemCopyBuffer;
            Assert.That(exported, Is.EqualTo(PhaseCustomMap.ToJson(editor.Room())));
            editor.ImportDraft("{\"blocks\":[{\"x\":0,\"y\":168,\"w\":320,\"h\":8,\"phaseMask\":7}],\"spawnX\":24,\"spawnY\":152}");
            Assert.That((editor.Room().platforms.Length, editor.Room().gears.Length), Is.EqualTo((0, 0)), "older maps import without mechanisms");
            editor.ImportDraft(exported);
            Assert.That(PhaseCustomMap.ToJson(editor.Room()), Is.EqualTo(exported));

            yield return Tap(Key.Enter);
            var sandbox = editor.Playtest;
            Assert.That((sandbox.Controller.movers.Length, sandbox.Controller.gears.Length), Is.EqualTo((1, 1)));
            Assert.That((sandbox.Controller.movers[0].x, sandbox.Controller.movers[0].y, sandbox.Controller.gears[0].wall.x, sandbox.Controller.gears[0].platform.y),
                Is.EqualTo((64.0, 112.0, 224.0, 128.0)), "playtest mechanisms start at their authored cells");
            yield return Tap(Key.Escape);
            yield return Tap(Key.Escape);
            yield return WaitFor(PhaseTitle.Scene);
            yield return Tap(Key.RightArrow); yield return Tap(Key.Z);
            yield return WaitFor(PhaseEditor.Scene);
            Assert.That(PhaseCustomMap.ToJson(Object.FindAnyObjectByType<PhaseEditor>().Room()), Is.EqualTo(exported), "platforms and gears survive in the draft");
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (saved == null) PlayerPrefs.DeleteKey(PhaseEditor.DraftKey); else PlayerPrefs.SetString(PhaseEditor.DraftKey, saved);
            PlayerPrefs.Save(); GUIUtility.systemCopyBuffer = clipboard;
        }
    }
}
