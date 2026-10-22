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
    // Editor PlayerPrefs are shared by every open Editor of this project, so start from the default keys rather than whatever they hold.
    [SetUp] public void DefaultKeys() => PhaseSandbox.ApplyControls(PhaseSandbox.DefaultSlots);
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
            Assert.That(editor.World().rooms[0].blocks.Length, Is.EqualTo(PhaseEditor.Columns), "new draft starts with a floor");
            yield return Tap(Key.Digit2);
            yield return Tap(Key.RightArrow);
            Assert.That((editor.Mask, editor.Cursor), Is.EqualTo((1, new Vector2Int(9, 18))));
            yield return Tap(Key.Z);
            yield return Tap(Key.RightArrow);
            yield return Tap(Key.X);
            Assert.That(editor.Tool, Is.EqualTo(2));
            yield return Tap(Key.Z);
            yield return Tap(Key.X);
            Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.LaserTool));
            yield return Tap(Key.RightArrow); yield return Tap(Key.Z);
            yield return Tap(Key.X);
            Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.CrystalTool));
            yield return Tap(Key.Digit5); yield return Tap(Key.RightArrow); yield return Tap(Key.Z);
            var room = editor.World().rooms[0];
            Assert.That(room.blocks.Any(b => b.x == 72 && b.y == 144 && b.phaseMask == 1), Is.True, "jump paints red terrain");
            var spike = room.spikes.Single();
            Assert.That((spike.x, spike.y, spike.direction, spike.phaseMask), Is.EqualTo((80.0, 144.0, SpikeDirection.Up, 1)));
            var laser = room.lasers.Single();
            Assert.That((laser.x, laser.y, laser.direction, laser.phaseMask), Is.EqualTo((88.0, 144.0, LaserDirection.Up, 1)));
            var crystal = room.crystals.Single();
            Assert.That((crystal.x, crystal.y, crystal.phaseMask), Is.EqualTo((96.0, 144.0, 5)));

            // Outside Web the import/export icons use the clipboard.
            Assert.That(editor.ExportDraft(), Is.True);
            string exported = GUIUtility.systemCopyBuffer, draft = PhaseCustomMap.ToJson(editor.World());
            Assert.That(exported, Is.EqualTo(draft));
            editor.ImportDraft("{");
            Assert.That(PhaseCustomMap.ToJson(editor.World()), Is.EqualTo(draft), "an invalid file leaves the draft unchanged");
            editor.ImportDraft("{\"name\":\"x\",\"blocks\":[{\"x\":0,\"y\":168,\"w\":320,\"h\":8,\"phaseMask\":2}],\"spawnX\":40,\"spawnY\":152}");
            Assert.That((editor.World().rooms[0].blocks.Length, editor.World().rooms[0].blocks[0].phaseMask, Spawn(editor)), Is.EqualTo((PhaseEditor.Columns, 2, new Vector2Int(5, 19))), "wide blocks import as cells");
            editor.ImportDraft(exported);
            Assert.That(PhaseCustomMap.ToJson(editor.World()), Is.EqualTo(draft), "export then import restores the draft");

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
            room = Object.FindAnyObjectByType<PhaseEditor>().World().rooms[0];
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

    [UnityTest]
    public IEnumerator EditorPlacesJumpPlatformsSpikeRowsAndCover()
    {
        string saved = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null;
        PlayerPrefs.DeleteKey(PhaseEditor.DraftKey);
        try
        {
            yield return OpenTitle();
            yield return Tap(Key.RightArrow); yield return Tap(Key.RightArrow); yield return Tap(Key.Z);
            yield return WaitFor(PhaseEditor.Scene);
            var editor = Object.FindAnyObjectByType<PhaseEditor>();
            IEnumerator Cycle(int tool) { for (int i = 0; i < 12 && !(editor.Tool == tool || (tool == PhaseEditor.SpikeTool && editor.Tool >= 2 && editor.Tool <= 5)); i++) yield return Tap(Key.X); }
            IEnumerator Move(Key key, int cells) { for (int i = 0; i < cells; i++) yield return Tap(key); }
            // Platform: first pivot, a body end one cell away (length 3), a second pivot, then the first again closes the loop.
            yield return Cycle(PhaseEditor.PlatformTool);
            Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.PlatformTool));
            yield return Tap(Key.Z); yield return Move(Key.RightArrow, 1);
            Assert.That(editor.Placing, Is.True);
            yield return Tap(Key.Z); yield return Move(Key.RightArrow, 4); yield return Tap(Key.Z);
            Assert.That(editor.Placing, Is.True, "the loop stays open until the first pivot is clicked again");
            yield return Move(Key.LeftArrow, 5); yield return Tap(Key.Z);
            Assert.That(editor.Placing, Is.False);
            var platform = editor.World().rooms[0].platforms.Single();
            Assert.That((platform.vertical, platform.length, platform.reverse), Is.EqualTo((false, 3, false)));
            Assert.That(platform.pivots, Is.EqualTo(new[] { (8, 18), (13, 18) }));
            yield return Tap(Key.F);
            Assert.That(editor.World().rooms[0].platforms.Single().reverse, Is.True, "F flips the direction under the cursor");
            // The spike tool on the platform toggles the row on the face it points to; the cover tool covers it, or the terrain under it.
            yield return Cycle(PhaseEditor.SpikeTool); yield return Tap(Key.Z);
            Assert.That(editor.World().rooms[0].platforms.Single().spikes, Is.EqualTo(new[] { SpikeDirection.Up }));
            Assert.That(editor.World().rooms[0].spikes, Is.Empty, "the row belongs to the platform, not to a cell");
            yield return Cycle(PhaseEditor.CoverTool); Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.CoverTool));
            yield return Tap(Key.Z);
            Assert.That(editor.World().rooms[0].platforms.Single().spiked, Is.True);
            yield return Move(Key.DownArrow, 3); yield return Tap(Key.Z);
            Assert.That(editor.World().rooms[0].blocks.Count(b => b.spiked && b.x == 64 && b.y == 168 && b.phaseMask == 7), Is.EqualTo(1), "covering terrain keeps its color");
            yield return Tap(Key.Z);
            Assert.That(editor.World().rooms[0].blocks.Count(b => b.spiked), Is.Zero, "a second click uncovers it");
            // Refusals flash and change nothing: closing a loop with one pivot, a second click off the row and column, a start over a
            // platform, and spike rows over terrain or past the room's edge.
            IEnumerator GoTo(int x, int y)
            {
                while (editor.Cursor.x != x) yield return Tap(editor.Cursor.x < x ? Key.RightArrow : Key.LeftArrow);
                while (editor.Cursor.y != y) yield return Tap(editor.Cursor.y < y ? Key.DownArrow : Key.UpArrow);
            }
            yield return Cycle(PhaseEditor.PlatformTool);
            yield return Tap(Key.Z); yield return Tap(Key.Z); yield return Tap(Key.Z);
            Assert.That((editor.Placing, editor.World().rooms[0].platforms.Length), Is.EqualTo((true, 1)), "one pivot cannot close a loop");
            yield return Tap(Key.Escape);
            yield return Tap(Key.Z); yield return GoTo(9, 20); yield return Tap(Key.Z);
            Assert.That((editor.Placing, editor.DraftLength), Is.EqualTo((true, 0)), "a second click off the pivot's row and column is refused");
            yield return Tap(Key.Escape);
            yield return GoTo(8, 18); yield return Tap(Key.Z); yield return Tap(Key.Z); yield return GoTo(12, 16); yield return Tap(Key.Z);
            yield return GoTo(8, 18); yield return Tap(Key.Z);
            Assert.That((editor.Placing, editor.World().rooms[0].platforms.Length), Is.EqualTo((true, 1)), "a platform cannot start on another");
            yield return Tap(Key.Escape);
            yield return Cycle(PhaseEditor.Terrain); yield return GoTo(9, 19); yield return Tap(Key.Z);
            yield return Cycle(PhaseEditor.SpikeTool); yield return Tap(Key.R); yield return Tap(Key.R);
            Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.SpikeTool + (int)SpikeDirection.Down));
            yield return GoTo(8, 18); yield return Tap(Key.Z);
            Assert.That(editor.World().rooms[0].platforms[0].spikes, Is.EqualTo(new[] { SpikeDirection.Up }), "a spike row over terrain is refused");
            yield return Cycle(PhaseEditor.PlatformTool);
            yield return GoTo(20, 0); yield return Tap(Key.Z); yield return Tap(Key.Z); yield return GoTo(24, 0); yield return Tap(Key.Z);
            yield return GoTo(20, 0); yield return Tap(Key.Z);
            Assert.That(editor.World().rooms[0].platforms.Length, Is.EqualTo(2));
            yield return Cycle(PhaseEditor.SpikeTool); yield return Tap(Key.R); yield return Tap(Key.R);
            Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.SpikeTool + (int)SpikeDirection.Up));
            yield return Tap(Key.Z);
            Assert.That(editor.World().rooms[0].platforms[1].spikes, Is.Empty, "a spike row past the room's edge is refused");
            // Escape cancels a platform in progress instead of leaving the editor.
            yield return Cycle(PhaseEditor.PlatformTool); yield return Tap(Key.Z);
            Assert.That(editor.Placing, Is.True);
            yield return Tap(Key.Escape);
            Assert.That((editor.Placing, SceneManager.GetActiveScene().name), Is.EqualTo((false, PhaseEditor.Scene)));
            yield return Tap(Key.Enter);
            var sandbox = editor.Playtest;
            Assert.That(sandbox, Is.Not.Null);
            Assert.That((sandbox.Controller.platforms.Count, sandbox.Controller.platforms[0].attached.Length, sandbox.Controller.platforms[0].spiked), Is.EqualTo((2, 1, true)));
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (saved == null) PlayerPrefs.DeleteKey(PhaseEditor.DraftKey); else PlayerPrefs.SetString(PhaseEditor.DraftKey, saved);
            PlayerPrefs.Save();
        }
    }

    [UnityTest]
    public IEnumerator EditorTurnsSpikesAndLasersWithRAndE()
    {
        string saved = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null;
        PlayerPrefs.DeleteKey(PhaseEditor.DraftKey);
        try
        {
            yield return OpenTitle();
            yield return Tap(Key.RightArrow); yield return Tap(Key.RightArrow); yield return Tap(Key.Z);
            yield return WaitFor(PhaseEditor.Scene);
            var editor = Object.FindAnyObjectByType<PhaseEditor>();
            yield return Tap(Key.R);
            Assert.That((editor.Tool, editor.Facing), Is.EqualTo((PhaseEditor.Terrain, 0)), "R leaves terrain alone");
            yield return Tap(Key.X); yield return Tap(Key.R);
            Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.SpikeTool + (int)SpikeDirection.Right), "R turns clockwise");
            yield return Tap(Key.Z);
            yield return Tap(Key.RightArrow); yield return Tap(Key.E); yield return Tap(Key.E); yield return Tap(Key.Z);
            yield return Tap(Key.X);
            Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.LaserTool + (int)LaserDirection.Left), "the laser keeps the spike's direction");
            yield return Tap(Key.RightArrow); yield return Tap(Key.Z);
            yield return Tap(Key.RightArrow); yield return Tap(Key.R); yield return Tap(Key.Z);
            yield return Tap(Key.Escape);
            yield return WaitFor(PhaseTitle.Scene);
            yield return Tap(Key.RightArrow); yield return Tap(Key.RightArrow); yield return Tap(Key.Z);
            yield return WaitFor(PhaseEditor.Scene);
            var room = Object.FindAnyObjectByType<PhaseEditor>().World().rooms[0];
            Assert.That(room.spikes.Select(s => (s.x, s.direction)), Is.EqualTo(new[] { (64.0, SpikeDirection.Right), (72.0, SpikeDirection.Left) }), "the draft keeps each spike's direction");
            Assert.That(room.lasers.Select(l => (l.x, l.direction)), Is.EqualTo(new[] { (80.0, LaserDirection.Left), (88.0, LaserDirection.Up) }), "the draft keeps each laser's direction");
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (saved == null) PlayerPrefs.DeleteKey(PhaseEditor.DraftKey); else PlayerPrefs.SetString(PhaseEditor.DraftKey, saved);
            PlayerPrefs.Save();
        }
    }

    static Vector2Int Spawn(PhaseEditor editor) { var room = editor.World().rooms[0]; return new Vector2Int((int)room.spawnX / 8, (int)room.spawnY / 8); }
    Mouse mouse;
    // Points at a frame pixel's center, inverting PhaseScreen.FramePoint, and holds the given buttons for two frames.
    IEnumerator Point(Vector2Int view, Vector2Int frame, params MouseButton[] buttons)
    {
        var state = new MouseState { position = new Vector2((frame.x + .5f) * Screen.width / view.x, Screen.height - (frame.y + .5f) * Screen.height / view.y) };
        foreach (var button in buttons) state = state.WithButton(button);
        InputSystem.QueueStateEvent(mouse, state); yield return null; yield return null;
    }
    static Vector2Int Center(RectInt rect) => new Vector2Int(rect.x + rect.width / 2, rect.y + rect.height / 2);
    [UnityTest]
    public IEnumerator EditorPanelStaysFixedAndTakesMouseClicks()
    {
        // Every layout keeps the screen's aspect and puts 18-pixel slots inside the view, beside or below the grid and never on it.
        foreach (var (width, height, side) in new[] { (960, 540, true), (2560, 1080, true), (390, 844, false) })
        {
            var view = PhaseEditor.ViewFor(width, height);
            var grid = new RectInt(0, -((180 - view.y) / 2), 320, 176);
            Assert.That(Mathf.Abs(view.x * height - view.y * width), Is.LessThan(Mathf.Max(width, height)), $"{width}x{height} keeps its aspect");
            var panel = PhaseEditor.PanelIn(view);
            Assert.That(side ? panel.xMin >= grid.xMax : panel.yMin >= grid.yMax, Is.True, $"{width}x{height} panel position");
            var cells = Enumerable.Range(0, PhaseEditor.SlotCount).Select(slot => PhaseEditor.SlotIn(view, slot)).ToList();
            foreach (var cell in cells)
                Assert.That((cell.width, cell.height, cell.xMin >= panel.xMin && cell.yMin >= panel.yMin && cell.xMax <= panel.xMax && cell.yMax <= panel.yMax,
                    panel.xMin >= 0 && panel.yMin >= 0 && panel.xMax <= view.x && panel.yMax <= view.y, cell.Overlaps(grid), cells.Count(c => c.Overlaps(cell))),
                    Is.EqualTo((18, 18, true, true, false, 1)), $"{width}x{height} slot {cells.IndexOf(cell)}");
        }

        string saved = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null;
        PlayerPrefs.DeleteKey(PhaseEditor.DraftKey);
        try
        {
            yield return OpenTitle();
            yield return Tap(Key.RightArrow); yield return Tap(Key.RightArrow); yield return Tap(Key.Z);
            yield return WaitFor(PhaseEditor.Scene);
            var editor = Object.FindAnyObjectByType<PhaseEditor>();
            mouse = InputSystem.AddDevice<Mouse>();
            var view = PhaseEditor.ViewFor(Screen.width, Screen.height);
            var texture = editor.GetComponentInChildren<SpriteRenderer>().sprite.texture;
            Assert.That((texture.width, texture.height), Is.EqualTo((view.x, view.y)), "the editor draws its own wider view");
            var panel = PhaseEditor.PanelIn(view);
            Color[] Panel() => texture.GetPixels(panel.x, view.y - panel.yMax, panel.width, panel.height);
            Vector2Int Cell(int x, int y) => new Vector2Int(x * 8 + 4, y * 8 + 4 - (180 - view.y) / 2);

            // The pointer sweeps the top and bottom rows: the cursor follows and the panel's pixels never change.
            var laserSlot = Center(PhaseEditor.SlotIn(view, 2));
            yield return Point(view, laserSlot, MouseButton.Left); yield return Point(view, laserSlot);
            Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.LaserTool));
            yield return Point(view, Cell(5, 0));
            var still = Panel();
            foreach (var cell in new[] { new Vector2Int(30, 0), new Vector2Int(39, 0), new Vector2Int(30, 21), new Vector2Int(0, 21), new Vector2Int(5, 0) })
            {
                yield return Point(view, Cell(cell.x, cell.y));
                Assert.That((editor.Cursor, editor.Hover), Is.EqualTo((cell, (int?)null)));
                Assert.That(Panel(), Is.EqualTo(still), $"the panel stays put with the cursor at {cell}");
            }

            // Hovering a slot highlights it; a click selects its tool or color and never paints.
            string draft = PhaseCustomMap.ToJson(editor.World());
            var tools = new System.Collections.Generic.List<int>(); var masks = new System.Collections.Generic.List<int>();
            for (int slot = 0; slot < PhaseEditor.ImportSlot; slot++)
            {
                var at = Center(PhaseEditor.SlotIn(view, slot));
                yield return Point(view, at);
                Assert.That(editor.Hover, Is.EqualTo(slot));
                var hovered = Panel();
                Assert.That(hovered, Is.Not.EqualTo(still), $"slot {slot} shows a hover highlight");
                yield return Point(view, at, MouseButton.Left); yield return Point(view, at);
                if (slot < PhaseEditor.MaskSlot) tools.Add(editor.Tool); else masks.Add(editor.Mask);
                still = Panel();
            }
            Assert.That(tools, Is.EqualTo(tools.Distinct()).And.EquivalentTo(new[] { 0, 1, 2, 6, 7, 11, 13, 14, 15, 16, 17 }), "each type slot picks its own tool");
            Assert.That(masks, Is.EqualTo(masks.Distinct()).And.EquivalentTo(Enumerable.Range(1, 7)), "each color slot picks its own color");
            Assert.That(PhaseCustomMap.ToJson(editor.World()), Is.EqualTo(draft), "panel clicks never paint");

            // A drag that starts on the panel never paints; strokes that start on the grid paint and erase.
            var terrain = Center(PhaseEditor.SlotIn(view, 0)); var blue = Center(PhaseEditor.SlotIn(view, PhaseEditor.MaskSlot + 2));
            yield return Point(view, blue, MouseButton.Left); yield return Point(view, blue);
            yield return Point(view, terrain, MouseButton.Left);
            yield return Point(view, Cell(10, 10), MouseButton.Left); yield return Point(view, Cell(11, 10), MouseButton.Left); yield return Point(view, Cell(11, 10));
            Assert.That((editor.Tool, editor.Mask, PhaseCustomMap.ToJson(editor.World())), Is.EqualTo((PhaseEditor.Terrain, 2, draft)), "a drag from the panel never paints");
            yield return Point(view, Cell(10, 10), MouseButton.Left); yield return Point(view, Cell(11, 10), MouseButton.Left); yield return Point(view, Cell(11, 10));
            Assert.That(editor.World().rooms[0].blocks.Count(b => b.y == 80 && (b.x == 80 || b.x == 88) && b.phaseMask == 2), Is.EqualTo(2), "a stroke on the grid paints");
            yield return Point(view, Cell(10, 10), MouseButton.Right); yield return Point(view, Cell(10, 10));
            Assert.That(editor.World().rooms[0].blocks.Count(b => b.y == 80), Is.EqualTo(1), "right-click erases");

            // Terrain over Wick's start refuses the playtest and blinks the spawn; clearing it lets the play slot start.
            var play = Center(PhaseEditor.SlotIn(view, PhaseEditor.PlaySlot)); var white = Center(PhaseEditor.SlotIn(view, PhaseEditor.MaskSlot));
            yield return Point(view, white, MouseButton.Left); yield return Point(view, white);
            yield return Point(view, Cell(Spawn(editor).x, Spawn(editor).y), MouseButton.Left); yield return Point(view, Cell(Spawn(editor).x, Spawn(editor).y));
            yield return Point(view, play, MouseButton.Left); yield return Point(view, play);
            Assert.That((editor.Playtest, editor.SpawnBlocked), Is.EqualTo(((PhaseSandbox)null, true)), "a blocked spawn refuses the playtest and blinks");
            var spawnCell = Spawn(editor);
            yield return Point(view, Cell(spawnCell.x, spawnCell.y), MouseButton.Right); yield return Point(view, Cell(spawnCell.x, spawnCell.y));
            Assert.That(editor.World().rooms[0].blocks.Any(b => b.x == spawnCell.x * 8 && b.y == spawnCell.y * 8), Is.False);
            // Erasing the cell also took the spawn; put it back.
            var flag = Center(PhaseEditor.SlotIn(view, 6));
            yield return Point(view, flag, MouseButton.Left); yield return Point(view, flag);
            yield return Point(view, Cell(spawnCell.x, spawnCell.y), MouseButton.Left); yield return Point(view, Cell(spawnCell.x, spawnCell.y));
            // The play and back slots work by mouse too; the playtest returns to the same view.
            yield return Point(view, play, MouseButton.Left); yield return Point(view, play);
            Assert.That(editor.Playtest, Is.Not.Null, "the play slot starts a playtest");
            yield return Tap(Key.Escape);
            Assert.That((editor.Playtest, texture.width, texture.height), Is.EqualTo(((PhaseSandbox)null, view.x, view.y)), "the playtest returns to the editor view");
            var back = Center(PhaseEditor.SlotIn(view, PhaseEditor.BackSlot));
            yield return Point(view, back, MouseButton.Left); yield return Point(view, back);
            yield return WaitFor(PhaseTitle.Scene);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (saved == null) PlayerPrefs.DeleteKey(PhaseEditor.DraftKey); else PlayerPrefs.SetString(PhaseEditor.DraftKey, saved);
            PlayerPrefs.Save();
        }
    }

    // The editor scene with a fresh draft, a mouse, and helpers that address the frame by world cell.
    sealed class EditorRig
    {
        public PhaseEditor editor; public Vector2Int view;
        public Vector2Int At(int x, int y) => new Vector2Int((x - editor.Scroll.x) * 8 + 4, (y - editor.Scroll.y) * 8 + 4 - (180 - view.y) / 2);
        public Vector2Int Slot(int slot) => Center(PhaseEditor.SlotIn(view, slot));
    }
    IEnumerator OpenEditor(EditorRig rig)
    {
        yield return OpenTitle();
        yield return Tap(Key.RightArrow); yield return Tap(Key.RightArrow); yield return Tap(Key.Z);
        yield return WaitFor(PhaseEditor.Scene);
        rig.editor = Object.FindAnyObjectByType<PhaseEditor>(); mouse = InputSystem.AddDevice<Mouse>();
        rig.view = PhaseEditor.ViewFor(Screen.width, Screen.height);
    }
    const int TerrainSlot = 0, SpawnSlot = 6, EraseSlot = 7, GoalSlot = 8, RoomSlot = 9, MoveSlot = 10;
    IEnumerator Click(EditorRig rig, Vector2Int at) { yield return Point(rig.view, at, MouseButton.Left); yield return Point(rig.view, at); }
    IEnumerator ClickCell(EditorRig rig, int x, int y) { yield return Click(rig, rig.At(x, y)); }
    IEnumerator Pick(EditorRig rig, int slot) { yield return Click(rig, rig.Slot(slot)); }
    // Scrolls the window by dragging with the middle button, in strokes that stay inside it.
    IEnumerator Pan(EditorRig rig, int cells)
    {
        while (cells != 0)
        {
            int step = Mathf.Clamp(cells, -35, 35); var from = new Vector2Int(step > 0 ? 300 : 20, 88); var to = from - new Vector2Int(step * 8, 0);
            yield return Point(rig.view, from, MouseButton.Middle); yield return Point(rig.view, to, MouseButton.Middle); yield return Point(rig.view, to);
            cells -= step;
        }
    }
    IEnumerator Drag(EditorRig rig, Vector2Int from, Vector2Int to)
    {
        yield return Point(rig.view, rig.At(from.x, from.y), MouseButton.Left); yield return Point(rig.view, rig.At(to.x, to.y), MouseButton.Left); yield return Point(rig.view, rig.At(to.x, to.y));
    }
    static string Layout(PhaseEditor editor) => string.Join(" ", editor.World().rooms.Select(r => $"({r.atX},{r.atY},{r.GridWidth / 320},{r.GridHeight / 176})"));
    static string Rooms(params (int x, int y, int w, int h)[] rooms) => string.Join(" ", rooms.Select(r => $"({r.x},{r.y},{r.w},{r.h})"));

    [UnityTest]
    public IEnumerator EditorCreatesMergesSplitsMovesAndErasesRooms()
    {
        string saved = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null;
        PlayerPrefs.DeleteKey(PhaseEditor.DraftKey);
        try
        {
            var rig = new EditorRig(); yield return OpenEditor(rig); var editor = rig.editor;
            Assert.That((editor.RoomCount, Layout(editor)), Is.EqualTo((1, Rooms((0, 0, 1, 1)))), "a new draft is one screen");

            // Painting into a screen with no room makes a 1x1 room there; erasing its only cell makes it go away.
            yield return Pan(rig, 35);
            Assert.That(editor.Scroll.x, Is.EqualTo(35));
            yield return ClickCell(rig, 45, 10);
            Assert.That(Layout(editor), Is.EqualTo(Rooms((0, 0, 1, 1), (1, 0, 1, 1))), "painting beside the room makes a room");
            Assert.That(editor.World().rooms[1].blocks.Length, Is.EqualTo(1));
            yield return Pick(rig, EraseSlot); yield return ClickCell(rig, 45, 10);
            Assert.That(editor.RoomCount, Is.EqualTo(1), "a room with nothing left is removed");
            yield return Pick(rig, TerrainSlot); yield return ClickCell(rig, 45, 10);
            Assert.That(editor.RoomCount, Is.EqualTo(2));

            // The room tool on the seam joins the two rooms, then splits them along the same line.
            yield return Pick(rig, RoomSlot); yield return ClickCell(rig, 40, 10);
            var merged = editor.World().rooms.Single();
            Assert.That((editor.RoomCount, merged.atX, merged.GridWidth, merged.GridHeight, merged.blocks.Length), Is.EqualTo((1, 0, 640, 176, 41)), "merging keeps every cell");
            Assert.That(merged.blocks.Any(b => b.x == 360 && b.y == 80), Is.True, "and where it was");
            yield return ClickCell(rig, 40, 10);
            Assert.That(Layout(editor), Is.EqualTo(Rooms((0, 0, 1, 1), (1, 0, 1, 1))), "splitting restores the two rooms");
            Assert.That((editor.World().rooms[0].blocks.Length, editor.World().rooms[1].blocks.Length), Is.EqualTo((40, 1)));
            Assert.That(editor.World().rooms[0].spawnX, Is.EqualTo(24d), "the spawn stays with its side");

            // A seam with a room on one side only does nothing.
            yield return ClickCell(rig, 79, 10);
            Assert.That(editor.RoomCount, Is.EqualTo(2));

            // The move tool carries a room by whole screens; a drop onto another room is refused and changes nothing.
            yield return Pick(rig, MoveSlot);
            yield return Drag(rig, new Vector2Int(5, 5), new Vector2Int(29, 5));
            Assert.That((Layout(editor), editor.Flashing), Is.EqualTo((Rooms((0, 0, 1, 1), (1, 0, 1, 1)), true)), "dropping onto a room is refused");
            yield return new WaitForSeconds(.5f);
            yield return Drag(rig, new Vector2Int(45, 5), new Vector2Int(45, 17));
            Assert.That(Layout(editor), Is.EqualTo(Rooms((0, 0, 1, 1), (1, 1, 1, 1))), "a drag of more than half a screen moves it one screen");
            yield return Tap(Key.LeftCtrl, Key.UpArrow);
            Assert.That(Layout(editor), Is.EqualTo(Rooms((0, 0, 1, 1), (1, 0, 1, 1))), "Ctrl with an arrow moves the chosen room one screen");
            // Shift picks several rooms, and they move together.
            yield return ClickCell(rig, 37, 5);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift)); yield return null;
            yield return ClickCell(rig, 45, 5);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(editor.Selection, Is.EqualTo(new[] { 0, 1 }));
            yield return Tap(Key.LeftCtrl, Key.RightArrow);
            Assert.That(Layout(editor), Is.EqualTo(Rooms((1, 0, 1, 1), (2, 0, 1, 1))), "both moved one screen right");
            yield return Tap(Key.LeftCtrl, Key.DownArrow);
            Assert.That(Layout(editor), Is.EqualTo(Rooms((1, 1, 1, 1), (2, 1, 1, 1))));
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (saved == null) PlayerPrefs.DeleteKey(PhaseEditor.DraftKey); else PlayerPrefs.SetString(PhaseEditor.DraftKey, saved);
            PlayerPrefs.Save();
        }
    }

    [UnityTest]
    public IEnumerator EditorPlacesSpawnsAndOneGoalAndPlaytestsTheViewedRoom()
    {
        string saved = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null, clipboard = GUIUtility.systemCopyBuffer;
        PlayerPrefs.DeleteKey(PhaseEditor.DraftKey);
        try
        {
            var rig = new EditorRig(); yield return OpenEditor(rig); var editor = rig.editor;
            // A room keeps as many spawns as it is given; clicking one again takes it back.
            yield return Pick(rig, SpawnSlot); yield return ClickCell(rig, 30, 5);
            Assert.That(editor.World().rooms[0].extraSpawns.Select(p => (p.x, p.y)), Is.EqualTo(new[] { (240.0, 40.0) }), "a second spawn");
            yield return ClickCell(rig, 30, 5);
            Assert.That(editor.World().rooms[0].extraSpawns, Is.Null);
            yield return ClickCell(rig, 30, 5);
            // The world has one goal: a second is refused, in the same room or another.
            yield return Pick(rig, GoalSlot); yield return ClickCell(rig, 20, 10);
            Assert.That(editor.World().rooms[0].goal.x, Is.EqualTo(160));
            yield return ClickCell(rig, 25, 10);
            Assert.That((editor.World().rooms[0].goal.x, editor.Flashing), Is.EqualTo((200d, false)), "the goal moves within its room");
            yield return ClickCell(rig, 25, 21);
            Assert.That((editor.World().rooms[0].goal.x, editor.World().rooms[0].goal.y, editor.Flashing), Is.EqualTo((200d, 80d, true)), "a goal onto terrain is refused");
            yield return new WaitForSeconds(.5f);

            // A room made by painting has no spawn: playtest and export are refused with its frame flashing.
            yield return Pan(rig, 35); yield return Pick(rig, TerrainSlot);
            yield return ClickCell(rig, 45, 21); yield return ClickCell(rig, 46, 21);
            Assert.That(editor.RoomCount, Is.EqualTo(2));
            yield return Pick(rig, GoalSlot); yield return ClickCell(rig, 50, 10);
            Assert.That(editor.World().rooms[1].goal, Is.Null, "nor in another room");
            yield return new WaitForSeconds(.5f);
            GUIUtility.systemCopyBuffer = "unchanged";
            Assert.That((editor.StartPlaytest(), editor.ExportDraft()), Is.EqualTo((false, false)), "a room without a spawn refuses both");
            Assert.That((editor.Playtest, editor.Flashing, GUIUtility.systemCopyBuffer), Is.EqualTo(((PhaseSandbox)null, true, "unchanged")));
            yield return new WaitForSeconds(1.4f);

            // The playtest starts in the room under the middle of the window, at its nearest spawn.
            yield return Pick(rig, SpawnSlot); yield return ClickCell(rig, 45, 19); yield return ClickCell(rig, 74, 19);
            yield return Tap(Key.Enter);
            var sandbox = editor.Playtest;
            Assert.That(sandbox, Is.Not.Null);
            Assert.That((sandbox.World.Room, sandbox.Controller.spawnX, sandbox.Controller.spawnY), Is.EqualTo((1, 40d, 152d)), "the viewed room, nearest spawn");
            yield return Tap(Key.Escape);
            Assert.That(editor.Playtest, Is.Null);
            yield return Pan(rig, -35);
            yield return Tap(Key.Enter);
            Assert.That((editor.Playtest.World.Room, editor.Playtest.Controller.spawnX), Is.EqualTo((0, 240d)), "scrolled back: the first room, the spawn nearest the middle");
            yield return Tap(Key.Escape);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (saved == null) PlayerPrefs.DeleteKey(PhaseEditor.DraftKey); else PlayerPrefs.SetString(PhaseEditor.DraftKey, saved);
            PlayerPrefs.Save(); GUIUtility.systemCopyBuffer = clipboard;
        }
    }

    [UnityTest]
    public IEnumerator EditorDraftKeepsARoomWithoutASpawnAfterReload()
    {
        string saved = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null;
        PlayerPrefs.DeleteKey(PhaseEditor.DraftKey);
        try
        {
            var rig = new EditorRig(); yield return OpenEditor(rig); var editor = rig.editor;
            yield return Pan(rig, 35); yield return ClickCell(rig, 45, 21);
            Assert.That(editor.RoomCount, Is.EqualTo(2));
            Assert.That(editor.World().rooms[1].noSpawn, Is.True, "a new room has no spawn");
            yield return Tap(Key.Escape); yield return WaitFor(PhaseTitle.Scene);
            yield return Tap(Key.RightArrow); yield return Tap(Key.RightArrow); yield return Tap(Key.Z); yield return WaitFor(PhaseEditor.Scene);
            editor = Object.FindAnyObjectByType<PhaseEditor>();
            var world = editor.World();
            Assert.That((world.rooms.Length, world.rooms[1].noSpawn, world.rooms[1].Spawns.Count, world.rooms[0].Spawns.Count), Is.EqualTo((2, true, 0, 1)), "the reloaded draft still has no spawn there");
            Assert.That(editor.StartPlaytest(), Is.False, "and it still refuses to play");
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (saved == null) PlayerPrefs.DeleteKey(PhaseEditor.DraftKey); else PlayerPrefs.SetString(PhaseEditor.DraftKey, saved);
            PlayerPrefs.Save();
        }
    }

    static PhaseDemoRoom Plain(int x, int y, int w, int h) => new PhaseDemoRoom {
        atX = x, atY = y, width = w * 320, height = h * 176, spawnX = 24, spawnY = h * 176 - 24, blocks = new[] { new Block(0, h * 176 - 8, w * 320, 8, phaseMask: 7) } };

    [UnityTest]
    public IEnumerator EditorShowsABeamWhoseEmitterIsOffscreenAndRefusesASplitPastTheRoomLimit()
    {
        string saved = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null;
        PlayerPrefs.DeleteKey(PhaseEditor.DraftKey);
        try
        {
            var rig = new EditorRig(); yield return OpenEditor(rig); var editor = rig.editor;
            // A beam stops only at terrain solid in the laser's own phase.
            var room = Plain(0, 0, 1, 1); room.blocks = room.blocks.Append(new Block(160, 80, 8, 8, phaseMask: 1)).Append(new Block(200, 80, 8, 8, phaseMask: 1)).ToArray();
            room.lasers = new[] { new DemoLaser { x = 160, y = 0, direction = LaserDirection.Down, phaseMask = 2 }, new DemoLaser { x = 200, y = 0, direction = LaserDirection.Down, phaseMask = 1 } };
            editor.ImportDraft(PhaseCustomMap.ToJson(new PhaseWorld { name = "自訂地圖", rooms = new[] { room } }));
            var preview = editor.Preview();
            Assert.That(preview.lasers.Select(l => preview.LaserBeam(l).h), Is.EqualTo(new[] { 160.0, 72.0 }), "red terrain stops only the red beam");
            var tall = Plain(0, 0, 1, 2); tall.lasers = new[] { new DemoLaser { x = 160, y = 0, direction = LaserDirection.Down, phaseMask = 1 } };
            editor.ImportDraft(PhaseCustomMap.ToJson(new PhaseWorld { name = "自訂地圖", rooms = new[] { tall } }));
            Assert.That(editor.RoomCount, Is.EqualTo(1));
            var texture = editor.GetComponentInChildren<SpriteRenderer>().sprite.texture;
            Color Pixel(Vector2Int p) => texture.GetPixel(p.x, rig.view.y - 1 - p.y);
            // Scroll until the emitter is above the window: the beam below it is still drawn, and stops at the floor of its own room.
            for (int i = 0; i < 2; i++)
            {
                var from = new Vector2Int(160, 150); var to = from - new Vector2Int(0, 80);
                yield return Point(rig.view, from, MouseButton.Middle); yield return Point(rig.view, to, MouseButton.Middle); yield return Point(rig.view, to);
            }
            Assert.That(editor.Scroll.y, Is.EqualTo(20));
            Assert.That(Pixel(rig.At(20, 30)), Is.Not.EqualTo(Pixel(rig.At(26, 30))), "the beam shows although its emitter is offscreen");

            // Splitting a room that would make the 257th is refused before anything changes.
            var rooms = new System.Collections.Generic.List<PhaseDemoRoom> { Plain(0, 0, 2, 1) };
            for (int i = 0; i < 255; i++) rooms.Add(Plain(2 + i % 16, i / 16, 1, 1));
            editor.ImportDraft(PhaseCustomMap.ToJson(new PhaseWorld { name = "自訂地圖", rooms = rooms.ToArray() }));
            Assert.That(editor.RoomCount, Is.EqualTo(PhaseWorld.MaxRooms));
            string before = PhaseCustomMap.ToJson(editor.World());
            yield return Pick(rig, RoomSlot); yield return ClickCell(rig, 39, 10);
            Assert.That((editor.RoomCount, editor.Flashing), Is.EqualTo((PhaseWorld.MaxRooms, true)), "the split is refused with the flash");
            Assert.That(PhaseCustomMap.ToJson(editor.World()) == before, Is.True, "and nothing changed");
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (saved == null) PlayerPrefs.DeleteKey(PhaseEditor.DraftKey); else PlayerPrefs.SetString(PhaseEditor.DraftKey, saved);
            PlayerPrefs.Save();
        }
    }

    [UnityTest]
    public IEnumerator EditorKeepsPlatformsOnTheirPathsWhenScrolledRefusesASplitThroughASpawnAndSavesCompactDrafts()
    {
        string saved = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null;
        PlayerPrefs.DeleteKey(PhaseEditor.DraftKey);
        try
        {
            var rig = new EditorRig(); yield return OpenEditor(rig); var editor = rig.editor;
            var tall = Plain(0, 0, 1, 2);
            tall.platforms = new[] { new DemoPlatform { length = 3, pivots = new[] { (10, 40), (20, 40) }, spikes = new SpikeDirection[0] } };
            tall.spawnX = 24; tall.spawnY = 21 * 8;
            editor.ImportDraft(PhaseCustomMap.ToJson(new PhaseWorld { name = "自訂地圖", rooms = new[] { tall } }));
            Assert.That(editor.RoomCount, Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetString(PhaseEditor.DraftKey).Contains("\n"), Is.False, "drafts are saved compact");

            // Scrolled down, the platform body sits on its drawn path: it moved up by exactly the scroll.
            double y0 = editor.Preview().platforms[0].y; int scroll0 = editor.Scroll.y;
            for (int i = 0; i < 2; i++)
            {
                var from = new Vector2Int(160, 150); var to = from - new Vector2Int(0, 80);
                yield return Point(rig.view, from, MouseButton.Middle); yield return Point(rig.view, to, MouseButton.Middle); yield return Point(rig.view, to);
            }
            Assert.That(editor.Scroll.y, Is.GreaterThan(scroll0));
            Assert.That(editor.Preview().platforms[0].y, Is.EqualTo(y0 - 8 * (editor.Scroll.y - scroll0)), "the platform follows the scroll");
            for (int i = 0; i < 2; i++)
            {
                var from = new Vector2Int(160, 70); var to = from + new Vector2Int(0, 80);
                yield return Point(rig.view, from, MouseButton.Middle); yield return Point(rig.view, to, MouseButton.Middle); yield return Point(rig.view, to);
            }

            // A horizontal line between the two cells of the spawn's footprint is refused.
            string before = PhaseCustomMap.ToJson(editor.World());
            yield return Pick(rig, RoomSlot); yield return ClickCell(rig, 10, 22);
            Assert.That((editor.RoomCount, editor.Flashing), Is.EqualTo((1, true)), "a split through a spawn is refused");
            Assert.That(PhaseCustomMap.ToJson(editor.World()) == before, Is.True);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            if (saved == null) PlayerPrefs.DeleteKey(PhaseEditor.DraftKey); else PlayerPrefs.SetString(PhaseEditor.DraftKey, saved);
            PlayerPrefs.Save();
        }
    }

    [UnityTest]
    public IEnumerator EditorExportsAndImportsAMultiRoomWorldAndShowsItInTheOverview()
    {
        string saved = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null, clipboard = GUIUtility.systemCopyBuffer;
        PlayerPrefs.DeleteKey(PhaseEditor.DraftKey);
        try
        {
            var rig = new EditorRig(); yield return OpenEditor(rig); var editor = rig.editor;
            PhaseDemoRoom Room(int x, int y, int w, int h) => new PhaseDemoRoom {
                atX = x, atY = y, width = w * 320, height = h * 176, spawnX = 24, spawnY = h * 176 - 24, blocks = new[] { new Block(0, h * 176 - 8, w * 320, 8, phaseMask: 7) } };
            var left = Room(-1, 0, 1, 1); var middle = Room(0, 0, 2, 1); var above = Room(1, -2, 1, 2);
            middle.extraSpawns = new[] { new DemoPoint { x = 400, y = 152 } }; above.goal = new DemoPoint { x = 80, y = 80 };
            above.lasers = new[] { new DemoLaser { x = 8, y = 8, direction = LaserDirection.Down, phaseMask = 2 } };
            above.platforms = new[] { new DemoPlatform { length = 3, pivots = new[] { (10, 30), (20, 30) }, spikes = new[] { SpikeDirection.Up } } };
            string source = PhaseCustomMap.ToJson(new PhaseWorld { name = "自訂地圖", rooms = new[] { left, middle, above } });
            editor.ImportDraft(source);
            Assert.That((editor.RoomCount, editor.Flashing), Is.EqualTo((3, false)), "a world with several rooms, spawns and a goal imports whole");
            Assert.That(Layout(editor), Is.EqualTo(Rooms((-1, 0, 1, 1), (0, 0, 2, 1), (1, -2, 1, 2))));
            Assert.That(editor.ExportDraft(), Is.True);
            Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo(PhaseCustomMap.ToJson(PhaseCustomMap.Parse(source))), "export writes the same world");
            editor.ImportDraft("{"); Assert.That((editor.RoomCount, editor.Flashing), Is.EqualTo((3, true)), "an invalid file changes nothing");

            // Tab shows the whole world; a click in it scrolls the window there, and Tab or Escape closes it.
            yield return Tap(Key.Tab);
            Assert.That(editor.Overview, Is.True);
            yield return Tap(Key.Tab);
            Assert.That(editor.Overview, Is.False);
            yield return Tap(Key.Tab); yield return Tap(Key.Escape);
            Assert.That((editor.Overview, SceneManager.GetActiveScene().name), Is.EqualTo((false, PhaseEditor.Scene)));
            yield return Tap(Key.Tab);
            var before = editor.Scroll;
            var panel = PhaseEditor.PanelIn(rig.view);
            var area = new Vector2Int((panel.x > 100 ? panel.x : rig.view.x) / 2, (panel.x > 100 ? rig.view.y : panel.y) / 2);
            yield return Click(rig, area);
            Assert.That(editor.Overview, Is.False);
            // The world spans screens -1..2 across and -2..1 down, so its middle is cell (20, -11) and the window's corner (0, -22), give or take the pixel rounding.
            Assert.That(editor.Scroll.x, Is.EqualTo(0).Within(25));
            Assert.That(editor.Scroll.y, Is.EqualTo(-22).Within(25));
            Assert.That(editor.Scroll, Is.Not.EqualTo(before));
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
            Assert.That(keys, Is.Not.Null, "the gear opens settings");
            for (int i = 0; i < 2; i++) yield return Tap(Key.DownArrow);
            yield return Tap(Key.Enter);
            Assert.That(keys.Capturing, Is.True);
            yield return Tap(Key.J);
            Assert.That((keys.Capturing, PhaseSandbox.Slots[4]), Is.EqualTo((false, "J")), "the next key binds the jump slot");
            Assert.That(PlayerPrefs.GetString(PhaseSandbox.ControlsKey).Split(',')[4], Is.EqualTo("J"), "bindings persist");
            yield return Tap(Key.RightArrow); yield return Tap(Key.Enter); yield return Tap(Key.J);
            Assert.That((keys.Flashing, PhaseSandbox.Slots[5]), Is.EqualTo((true, "Space")), "a duplicate key flashes and is not applied");
            yield return Tap(Key.Enter); yield return Tap(Key.F1);
            Assert.That((keys.Flashing, PhaseSandbox.Slots[5]), Is.EqualTo((true, "Space")), "an unsupported key is not applied");
            yield return Tap(Key.Enter); yield return Tap(Key.Escape);
            Assert.That((keys.Capturing, sandbox.Keys, PhaseSandbox.Slots[5]), Is.EqualTo((false, keys, "Space")), "Escape cancels a capture");
            yield return Tap(Key.Backspace);
            Assert.That(PhaseSandbox.Slots[5], Is.Empty, "Backspace clears a slot");
            yield return new WaitForSeconds(.5f);
            yield return Tap(Key.LeftArrow); yield return Tap(Key.Backspace);
            Assert.That((keys.Flashing, PhaseSandbox.Slots[4]), Is.EqualTo((true, "J")), "the last key of an action stays");
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
        Assert.That((title.Choice, title.Keys != null), Is.EqualTo((3, true)), "title gear opens settings");
        yield return Tap(Key.Escape);
        Assert.That(title.Keys, Is.Null);
        yield return Tap(Key.LeftArrow); yield return Tap(Key.LeftArrow);
        yield return Tap(Key.Z);
        Assert.That(title.LevelSelect, Is.Not.Null, "title grid icon opens level select");
        yield return Tap(Key.Escape);
        Assert.That(title.LevelSelect, Is.Null);
        yield return Tap(Key.Enter);
        int columns = PhaseLevels.Columns(PhaseSandbox.ViewSize(Screen.width, Screen.height)), second = columns + 1;
        yield return Tap(Key.RightArrow); yield return Tap(Key.DownArrow);
        Assert.That(title.LevelSelect.Choice, Is.EqualTo(second));
        yield return Tap(Key.Enter);
        yield return WaitFor(PhaseSandbox.Levels[second]);

        yield return Tap(Key.Escape); yield return Tap(Key.RightArrow); yield return Tap(Key.Enter);
        var levels = Sandbox().LevelSelect;
        Assert.That(levels.Choice, Is.EqualTo(second), "pause level select starts at the current level");
        yield return Tap(Key.DownArrow); yield return Tap(Key.DownArrow); yield return Tap(Key.DownArrow);
        for (int i = 0; i < columns; i++) yield return Tap(Key.RightArrow);
        Assert.That(levels.Choice, Is.EqualTo(14), "the bottom row stops at the last level");
        yield return Tap(Key.Z);
        yield return WaitFor(PhaseSandbox.Levels[14]);

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

    // Lit pixels at the switch's off and on knob centers. The knob is cell-relative page pixels (19, 7) and (24, 7).
    static (bool off, bool on) Knob(PhaseKeys keys)
    {
        var size = PhaseSandbox.ViewSize(Screen.width, Screen.height);
        int scale = UiZoom.Fit(size, 67, 84).Scale;
        int ox = (size.x - 67 * scale) / 2, oy = (size.y - 84 * scale) / 2;
        var texture = new Texture2D(size.x, size.y);
        try
        {
            var frame = new PixelFrame(texture); keys.Draw(frame); frame.Apply();
            var pixels = texture.GetPixels32();
            bool Lit(int pageX, int pageY)
            {
                int x = ox + pageX * scale + scale / 2, y = oy + pageY * scale + scale / 2;
                return pixels[(size.y - 1 - y) * size.x + x].Equals(PixelFrame.Light);
            }
            return (Lit(17 + 19, 52 + 7), Lit(17 + 24, 52 + 7));
        }
        finally { Object.Destroy(texture); }
    }
    [UnityTest]
    public IEnumerator SettingsFullScreenSwitchFollowsKeysClicksAndScenes()
    {
        // Screen.fullScreen does nothing in the Editor, so the switch drives a stand-in here.
        var (isFull, setFull) = (PhaseKeys.IsFullScreen, PhaseKeys.SetFullScreen);
        bool full = false; int requests = 0;
        PhaseKeys.IsFullScreen = () => full; PhaseKeys.SetFullScreen = on => { full = on; requests++; };
        try
        {
            yield return OpenTitle();
            var title = Object.FindAnyObjectByType<PhaseTitle>();
            for (int i = 0; i < 3; i++) yield return Tap(Key.RightArrow);
            yield return Tap(Key.Z);
            var keys = title.Keys;
            Assert.That(keys, Is.Not.Null, "jump on the gear opens settings");
            for (int i = 0; i < PhaseKeys.FullScreenRow; i++) yield return Tap(Key.DownArrow);
            Assert.That((keys.Row, Knob(keys)), Is.EqualTo((PhaseKeys.FullScreenRow, (true, false))), "down reaches the fullscreen switch, off");
            yield return Tap(Key.Enter);
            Assert.That((full, Knob(keys)), Is.EqualTo((true, (false, true))), "Enter turns fullscreen on");
            yield return Tap(Key.Z);
            Assert.That((full, keys.Capturing), Is.EqualTo((false, false)), "jump turns it off");

            var mouse = InputSystem.AddDevice<Mouse>();
            var size = PhaseSandbox.ViewSize(Screen.width, Screen.height); var center = PhaseKeys.Area(size, PhaseKeys.FullScreenRow, 0).center;
            var point = new Vector2(center.x / size.x * Screen.width, (1 - center.y / size.y) * Screen.height);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            Assert.That(full, Is.True, "a click toggles the switch");

            // Fullscreen is player state, so a level's pause settings show the same switch.
            yield return Tap(Key.Escape);
            for (int i = 0; i < 3; i++) yield return Tap(Key.LeftArrow);
            yield return Tap(Key.Enter);
            yield return WaitFor("PhaseDemo1");
            var sandbox = Sandbox();
            yield return Tap(Key.Escape); yield return Tap(Key.RightArrow); yield return Tap(Key.RightArrow); yield return Tap(Key.Enter);
            keys = sandbox.Keys;
            for (int i = 0; i < PhaseKeys.FullScreenRow; i++) yield return Tap(Key.DownArrow);
            Assert.That(Knob(keys), Is.EqualTo((false, true)), "the switch stays on in another scene");
            yield return Tap(Key.Enter);
            Assert.That((full, requests), Is.EqualTo((false, 4)));
            LogAssert.NoUnexpectedReceived();
        }
        finally { PhaseKeys.IsFullScreen = isFull; PhaseKeys.SetFullScreen = setFull; }
    }

    IEnumerator Move(Key key, int count) { for (int i = 0; i < count; i++) yield return Tap(key); }

    static Vector2 ScreenPoint(Vector2Int size, int index)
    {
        int scale = UiZoom.Fit(size, 80, 44).Scale;
        int width = 3 * PhaseMenu.Pitch + PhaseMenu.Size;
        int middle = size.y / 2 + 11 * scale;
        int ox = (size.x - width * scale) / 2, oy = middle - PhaseMenu.Size * scale / 2;
        int x = ox + (index * PhaseMenu.Pitch + PhaseMenu.Size / 2) * scale + scale / 2;
        int y = oy + (PhaseMenu.Size / 2) * scale + scale / 2;
        return new Vector2(x / (float)size.x * Screen.width, (1f - y / (float)size.y) * Screen.height);
    }
    IEnumerator Click(Mouse mouse, Vector2 point)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null; yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
    }
    [UnityTest]
    public IEnumerator FirstClickQuickTapAndMouseReachTheMenus()
    {
        yield return SceneManager.LoadSceneAsync(PhaseTitle.Scene);
        keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        var title = Object.FindAnyObjectByType<PhaseTitle>();
        var size = PhaseSandbox.ViewSize(Screen.width, Screen.height);
        var before = title.GetComponentInChildren<SpriteRenderer>().sprite.texture.GetPixels32();
        // Hovering lights the icon under the pointer without moving the choice.
        InputSystem.QueueStateEvent(mouse, new MouseState { position = ScreenPoint(size, 1) }); yield return null; yield return null;
        Assert.That((title.Choice, title.Pointer), Is.EqualTo((0, 1)), "the pointer lights the level-select icon");
        var hovered = title.GetComponentInChildren<SpriteRenderer>().sprite.texture.GetPixels32();
        Assert.That(hovered.Count(p => p.Equals(PixelFrame.Light)), Is.GreaterThan(before.Count(p => p.Equals(PixelFrame.Light))), "the hovered icon is drawn at full light");
        before = hovered;
        title.SetControlsOpen("false");
        yield return Click(mouse, ScreenPoint(size, 1));
        Assert.That(title.LevelSelect, Is.Not.Null, "the click that clears the input gate opens level select");
        var after = title.GetComponentInChildren<SpriteRenderer>().sprite.texture.GetPixels32();
        int light = 0, replaced = 0;
        for (int i = 0; i < before.Length; i++) if (before[i].Equals(PixelFrame.Light)) { light++; if (!after[i].Equals(before[i])) replaced++; }
        Assert.That(replaced, Is.GreaterThan(light / 2), "level select replaces the title");
        for (int i = 0; i < PhaseSandbox.Levels.Length; i++)
        {
            var tile = PhaseLevels.Tile(size, i);
            Assert.That(tile.xMin >= 0 && tile.yMin >= 0 && tile.xMax <= size.x && tile.yMax <= size.y, Is.True, $"tile {i} stays inside the view");
        }
        yield return Tap(Key.Escape);
        Assert.That(title.LevelSelect, Is.Null);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        Assert.That(title.Choice, Is.EqualTo(0), "a tap shorter than a frame moves the selection");
        yield return Click(mouse, ScreenPoint(size, 3));
        var keys = title.Keys;
        Assert.That(keys, Is.Not.Null, "clicking the gear opens settings");
        var cap = PhaseKeys.Area(size, 2, 1).center;
        yield return Click(mouse, new Vector2(cap.x / size.x * Screen.width, (1f - cap.y / size.y) * Screen.height));
        Assert.That((keys.Capturing, keys.Row, keys.Column), Is.EqualTo((true, 2, 1)), "clicking a key cap captures that slot");
        LogAssert.NoUnexpectedReceived();
    }
}
