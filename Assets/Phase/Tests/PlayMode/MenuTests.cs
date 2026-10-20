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
            Assert.That(editor.Room().blocks.Length, Is.EqualTo(PhaseEditor.Columns), "new draft starts with a floor");
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
            yield return Tap(Key.X); yield return Tap(Key.X);
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
            var room = Object.FindAnyObjectByType<PhaseEditor>().Room();
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

            // With the gear's tall preview, the pointer sweeps the top and bottom rows: the cursor follows and the panel's pixels never change.
            var gear = Center(PhaseEditor.SlotIn(view, 6));
            yield return Point(view, gear, MouseButton.Left); yield return Point(view, gear);
            Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.GearTool));
            yield return Point(view, Cell(5, 0));
            var still = Panel();
            foreach (var cell in new[] { new Vector2Int(30, 0), new Vector2Int(39, 0), new Vector2Int(30, 21), new Vector2Int(0, 21), new Vector2Int(5, 0) })
            {
                yield return Point(view, Cell(cell.x, cell.y));
                Assert.That((editor.Cursor, editor.Hover), Is.EqualTo((cell, (int?)null)));
                Assert.That(Panel(), Is.EqualTo(still), $"the panel stays put with the cursor at {cell}");
            }

            // Hovering a slot highlights it; a click selects its tool or color and never paints.
            string draft = PhaseCustomMap.ToJson(editor.Room());
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
            Assert.That(tools, Is.EqualTo(tools.Distinct()).And.EquivalentTo(new[] { 0, 1, 2, 6, 7, 11, 12, PhaseEditor.PlatformTool, PhaseEditor.GearTool }), "each type slot picks its own tool");
            Assert.That(masks, Is.EqualTo(masks.Distinct()).And.EquivalentTo(Enumerable.Range(1, 7)), "each color slot picks its own color");
            Assert.That(PhaseCustomMap.ToJson(editor.Room()), Is.EqualTo(draft), "panel clicks never paint");

            // A drag that starts on the panel never paints; strokes that start on the grid paint and erase.
            var terrain = Center(PhaseEditor.SlotIn(view, 0)); var blue = Center(PhaseEditor.SlotIn(view, PhaseEditor.MaskSlot + 2));
            yield return Point(view, blue, MouseButton.Left); yield return Point(view, blue);
            yield return Point(view, terrain, MouseButton.Left);
            yield return Point(view, Cell(10, 10), MouseButton.Left); yield return Point(view, Cell(11, 10), MouseButton.Left); yield return Point(view, Cell(11, 10));
            Assert.That((editor.Tool, editor.Mask, PhaseCustomMap.ToJson(editor.Room())), Is.EqualTo((PhaseEditor.Terrain, 2, draft)), "a drag from the panel never paints");
            yield return Point(view, Cell(10, 10), MouseButton.Left); yield return Point(view, Cell(11, 10), MouseButton.Left); yield return Point(view, Cell(11, 10));
            Assert.That(editor.Room().blocks.Count(b => b.y == 80 && (b.x == 80 || b.x == 88) && b.phaseMask == 2), Is.EqualTo(2), "a stroke on the grid paints");
            yield return Point(view, Cell(10, 10), MouseButton.Right); yield return Point(view, Cell(10, 10));
            Assert.That(editor.Room().blocks.Count(b => b.y == 80), Is.EqualTo(1), "right-click erases");

            // Terrain over Wick's start refuses the playtest and blinks the spawn; clearing it lets the play slot start.
            var play = Center(PhaseEditor.SlotIn(view, PhaseEditor.PlaySlot)); var white = Center(PhaseEditor.SlotIn(view, PhaseEditor.MaskSlot));
            yield return Point(view, white, MouseButton.Left); yield return Point(view, white);
            yield return Point(view, Cell(editor.SpawnCell.x, editor.SpawnCell.y), MouseButton.Left); yield return Point(view, Cell(editor.SpawnCell.x, editor.SpawnCell.y));
            yield return Point(view, play, MouseButton.Left); yield return Point(view, play);
            Assert.That((editor.Playtest, editor.SpawnBlocked), Is.EqualTo(((PhaseSandbox)null, true)), "a blocked spawn refuses the playtest and blinks");
            yield return Point(view, Cell(editor.SpawnCell.x, editor.SpawnCell.y), MouseButton.Right); yield return Point(view, Cell(editor.SpawnCell.x, editor.SpawnCell.y));
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

    // Points at a frame pixel, as Point does, with the wheel turned by the given amount.
    IEnumerator Wheel(Vector2Int view, Vector2Int frame, Vector2 turn)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2((frame.x + .5f) * Screen.width / view.x, Screen.height - (frame.y + .5f) * Screen.height / view.y), scroll = turn });
        yield return null; yield return null;
    }
    [UnityTest]
    public IEnumerator EditorResizesAndScrollsALargeRoomThenExportsImportsAndPlaytestsIt()
    {
        string saved = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null, clipboard = GUIUtility.systemCopyBuffer;
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
            Vector2Int At(int x, int y) => new Vector2Int((x - editor.Scroll.x) * 8 + 4, (y - editor.Scroll.y) * 8 + 4 - (180 - view.y) / 2);
            IEnumerator Click(int slot) { var at = Center(PhaseEditor.SlotIn(view, slot)); yield return Point(view, at, MouseButton.Left); yield return Point(view, at); }
            Assert.That((editor.RoomColumns, editor.RoomRows, editor.Scroll), Is.EqualTo((40, 22, Vector2Int.zero)), "a new draft is one screen");
            yield return Wheel(view, At(5, 5), new Vector2(0, -1));
            Assert.That(editor.Scroll, Is.EqualTo(Vector2Int.zero), "a room that fits does not scroll");

            // The size slots grow the room by eight cells; the slot that cannot grow or shrink further changes nothing.
            yield return Click(PhaseEditor.NarrowerSlot);
            Assert.That((editor.RoomColumns, editor.RoomRows), Is.EqualTo((40, 22)), "a room is never smaller than one screen");
            for (int i = 0; i < 5; i++) yield return Click(PhaseEditor.WiderSlot);
            yield return Click(PhaseEditor.TallerSlot);
            Assert.That((editor.RoomColumns, editor.RoomRows), Is.EqualTo((80, 30)));
            Assert.That(editor.Room().blocks.Length, Is.EqualTo(PhaseEditor.Columns), "growing keeps the floor where it was");

            // The wheel scrolls rows, a sideways wheel or Ctrl+wheel columns, and a middle-button drag pans.
            yield return Wheel(view, At(5, 5), new Vector2(0, -1));
            Assert.That(editor.Scroll, Is.EqualTo(new Vector2Int(0, 2)));
            yield return Wheel(view, At(5, 5), new Vector2(1, 0));
            Assert.That(editor.Scroll, Is.EqualTo(new Vector2Int(2, 2)));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl)); yield return null;
            yield return Wheel(view, At(5, 5), new Vector2(0, -1));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(editor.Scroll, Is.EqualTo(new Vector2Int(4, 2)));
            var from = At(20, 10) + new Vector2Int(0, 0); var to = from - new Vector2Int(16, 8);
            yield return Point(view, from, MouseButton.Middle); yield return Point(view, to, MouseButton.Middle); yield return Point(view, to);
            Assert.That(editor.Scroll, Is.EqualTo(new Vector2Int(6, 3)), "dragging the room left and up reveals cells to the right and below");
            Assert.That(editor.Room().blocks.Length, Is.EqualTo(PhaseEditor.Columns), "scrolling paints nothing");
            for (int i = 0; i < 40 && editor.Scroll != new Vector2Int(40, 8); i++)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl)); yield return null;
                yield return Wheel(view, At(editor.Scroll.x + 5, editor.Scroll.y + 5), new Vector2(0, -1));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                if (editor.Scroll.x == 40) yield return Wheel(view, At(editor.Scroll.x + 5, editor.Scroll.y + 5), new Vector2(0, -1));
            }
            Assert.That(editor.Scroll, Is.EqualTo(new Vector2Int(40, 8)), "the view stops at the room's far corner");

            // The grid under the pointer is the scrolled cell: paint the last cell, check it drew there, then refuse to cut it off.
            Color Pixel(Vector2Int p) => texture.GetPixel(p.x, view.y - 1 - p.y);
            var corner = At(79, 29);
            yield return Point(view, corner, MouseButton.Left); yield return Point(view, corner);
            Assert.That(editor.Room().blocks.Any(b => b.x == 632 && b.y == 232 && b.phaseMask == 7), Is.True, "the far corner cell is paintable");
            Assert.That(Pixel(corner), Is.Not.EqualTo(Pixel(At(70, 25))), "the cell draws under the pointer");
            // A tall view also shows the cells just above the 40x22 window, and they take paint the same way.
            var above = At(79, editor.Scroll.y - 1);
            if (above.y >= 0)
            {
                yield return Point(view, above, MouseButton.Left); yield return Point(view, above);
                Assert.That(editor.Room().blocks.Any(b => b.x == 632 && b.y == 56), Is.True, "a cell shown above the window is paintable");
                yield return Point(view, above, MouseButton.Right); yield return Point(view, above);
            }
            yield return Click(PhaseEditor.NarrowerSlot);
            Assert.That(editor.RoomColumns, Is.EqualTo(80), "shrinking never deletes a cell");
            yield return Point(view, corner, MouseButton.Right); yield return Point(view, corner);
            yield return Click(PhaseEditor.NarrowerSlot);
            Assert.That((editor.RoomColumns, editor.Scroll.x), Is.EqualTo((72, 32)), "an empty strip can go, and the view stays in the room");

            // A floor strip to stand on, then export, import and playtest the 72x30 room.
            yield return Point(view, At(60, 29), MouseButton.Left);
            for (int x = 61; x < 72; x++) yield return Point(view, At(x, 29), MouseButton.Left);
            yield return Point(view, At(71, 29));
            Assert.That(editor.Room().blocks.Count(b => b.y == 232), Is.EqualTo(12));
            Assert.That(editor.ExportDraft(), Is.True);
            string exported = GUIUtility.systemCopyBuffer, draft = PhaseCustomMap.ToJson(editor.Room());
            Assert.That(exported, Is.EqualTo(draft));
            Assert.That(exported, Does.Contain("\"width\": 576").And.Contain("\"height\": 240"));
            editor.ImportDraft("{\"name\":\"x\",\"blocks\":[{\"x\":0,\"y\":168,\"w\":320,\"h\":8,\"phaseMask\":2}],\"spawnX\":40,\"spawnY\":152}");
            Assert.That((editor.RoomColumns, editor.RoomRows, editor.Scroll), Is.EqualTo((40, 22, Vector2Int.zero)), "an old file is one screen");
            editor.ImportDraft(exported);
            Assert.That((editor.RoomColumns, editor.RoomRows, PhaseCustomMap.ToJson(editor.Room())), Is.EqualTo((72, 30, draft)), "export then import restores the room");

            yield return Wheel(view, At(5, 5), new Vector2(0, -1));
            Assert.That(editor.Scroll, Is.EqualTo(new Vector2Int(0, 2)), "an imported room opens at its top-left corner");
            yield return Tap(Key.Enter);
            var sandbox = editor.Playtest;
            Assert.That(sandbox, Is.Not.Null);
            Assert.That((sandbox.roomSize, sandbox.Controller.Width, sandbox.Controller.Height), Is.EqualTo((new Vector2Int(576, 240), 576, 240)), "the playtest runs the whole room");
            var size = new Vector2Int(sandbox.Frame.width, sandbox.Frame.height);
            Vector2Int Camera() { var at = sandbox.pixelCamera.transform.position; return new Vector2Int(Mathf.RoundToInt(at.x - sandbox.Frame.width / 2f), Mathf.RoundToInt(-at.y - sandbox.Frame.height / 2f)); }
            Assert.That(Camera(), Is.EqualTo(new Vector2Int(0, Mathf.Max(0, 240 - size.y))), "the camera starts at the room's bottom-left");
            sandbox.Controller.player.x = 560; sandbox.Controller.player.y = 216;
            yield return new WaitForSeconds(2);
            Assert.That((Camera().x, sandbox.Controller.deaths), Is.EqualTo((576 - size.x, 0)), "the camera follows Wick to the room's right edge");
            yield return Tap(Key.Escape);
            Assert.That((editor.Playtest, editor.Scroll), Is.EqualTo(((PhaseSandbox)null, new Vector2Int(0, 2))), "the editor returns to the same view");

            // The draft keeps its size; one saved by an older build holds only a 40x22 grid and still opens.
            yield return Tap(Key.Escape); yield return WaitFor(PhaseTitle.Scene);
            yield return Tap(Key.RightArrow); yield return Tap(Key.RightArrow); yield return Tap(Key.Z); yield return WaitFor(PhaseEditor.Scene);
            editor = Object.FindAnyObjectByType<PhaseEditor>();
            Assert.That((editor.RoomColumns, editor.RoomRows, PhaseCustomMap.ToJson(editor.Room())), Is.EqualTo((72, 30, draft)), "the draft survives leaving the editor");
            yield return Tap(Key.Escape); yield return WaitFor(PhaseTitle.Scene);
            PlayerPrefs.SetString(PhaseEditor.DraftKey, "3,19;" + string.Concat(Enumerable.Repeat("00", 840)) + string.Concat(Enumerable.Repeat("17", 40)));
            yield return Tap(Key.RightArrow); yield return Tap(Key.RightArrow); yield return Tap(Key.Z); yield return WaitFor(PhaseEditor.Scene);
            editor = Object.FindAnyObjectByType<PhaseEditor>();
            Assert.That((editor.RoomColumns, editor.RoomRows, editor.Room().blocks.Length, editor.SpawnCell), Is.EqualTo((40, 22, 40, new Vector2Int(3, 19))), "an older draft is one screen");
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
        int scale = UiZoom.Fit(size, 67, 128).Scale;
        int ox = (size.x - 67 * scale) / 2, oy = (size.y - 128 * scale) / 2;
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
            return (Lit(17 + 19, 96 + 7), Lit(17 + 24, 96 + 7));
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
    [UnityTest]
    public IEnumerator EditorDrawsAPlatformLineThenPlaytestsTheDiagonal()
    {
        string saved = PlayerPrefs.HasKey(PhaseEditor.DraftKey) ? PlayerPrefs.GetString(PhaseEditor.DraftKey) : null, clipboard = GUIUtility.systemCopyBuffer;
        PlayerPrefs.DeleteKey(PhaseEditor.DraftKey);
        try
        {
            yield return OpenTitle();
            yield return Tap(Key.RightArrow); yield return Tap(Key.RightArrow); yield return Tap(Key.Z);
            yield return WaitFor(PhaseEditor.Scene);
            var editor = Object.FindAnyObjectByType<PhaseEditor>();
            yield return Move(Key.X, 5);
            Assert.That(editor.Tool, Is.EqualTo(PhaseEditor.PlatformTool));
            yield return Move(Key.UpArrow, 4);
            yield return Tap(Key.Z);
            Assert.That((editor.Anchor, editor.Room().platforms.Length), Is.EqualTo(((Vector2Int?)new Vector2Int(8, 14), 0)), "the first press places only the start");
            yield return Tap(Key.Escape);
            Assert.That((editor.Anchor, Object.FindAnyObjectByType<PhaseEditor>()), Is.EqualTo(((Vector2Int?)null, editor)), "escape drops a start that has no line");
            yield return Tap(Key.Z); yield return Move(Key.RightArrow, 4); yield return Move(Key.UpArrow, 1); yield return Tap(Key.Z);
            var platform = editor.Room().platforms.Single();
            Assert.That((string.Join(",", platform.nodeX.Concat(platform.nodeY)), platform.w, platform.h, platform.returns, editor.Anchor),
                Is.EqualTo(("64,96,112,104", 24.0, 8.0, false, (Vector2Int?)new Vector2Int(8, 14))), "the next press keeps both axes");
            yield return Move(Key.RightArrow, 3); yield return Move(Key.DownArrow, 2); yield return Tap(Key.Z);
            platform = editor.Room().platforms.Single();
            Assert.That(string.Join(",", platform.nodeX.Concat(platform.nodeY)), Is.EqualTo("64,96,120,112,104,120"), "another press extends the same line");
            yield return Tap(Key.C);
            Assert.That((editor.Anchor, editor.Room().platforms.Single().nodeX.Length), Is.EqualTo(((Vector2Int?)null, 3)), "grab ends the line");

            yield return Move(Key.RightArrow, 5); yield return Move(Key.UpArrow, 3);
            yield return Tap(Key.Z); yield return Tap(Key.C);
            Assert.That((editor.Anchor, editor.Room().platforms.Length), Is.EqualTo(((Vector2Int?)null, 1)), "grab drops a new start");
            yield return Tap(Key.Z); yield return Move(Key.RightArrow, 4); yield return Move(Key.DownArrow, 2); yield return Tap(Key.Z);
            platform = editor.Room().platforms.Last();
            Assert.That((string.Join(",", platform.nodeX.Concat(platform.nodeY)), platform.returns), Is.EqualTo(("160,192,96,112", false)));

            yield return Tap(Key.X);
            Assert.That((editor.Tool, editor.Anchor), Is.EqualTo((PhaseEditor.GearTool, (Vector2Int?)null)));
            yield return Tap(Key.Z); yield return Move(Key.RightArrow, 4); yield return Move(Key.DownArrow, 2); yield return Tap(Key.Z);
            var gear = editor.Room().gears.Single();
            Assert.That((gear.wall.x, gear.wall.y, gear.wall.w, gear.wall.h, gear.top, gear.platform.x, gear.platform.y, gear.platform.w, gear.platform.h, gear.endX),
                Is.EqualTo((192.0, 112.0, 8.0, 56.0, 96.0, 208.0, 128.0, 56.0, 8.0, 224.0)));

            yield return Move(Key.LeftArrow, 19); yield return Move(Key.UpArrow, 2);
            Assert.That(editor.Cursor, Is.EqualTo(new Vector2Int(9, 14)));
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
                Is.EqualTo((160.0, 96.0, 192.0, 128.0)), "playtest mechanisms start at their authored cells");
            int frame = Time.frameCount; double time = sandbox.Controller.time, x = sandbox.Controller.movers[0].x, y = sandbox.Controller.movers[0].y;
            yield return Tap(Key.X);
            yield return new WaitForSeconds(.45f);
            Assert.That(Time.frameCount, Is.GreaterThan(frame));
            Assert.That(sandbox.Controller.time, Is.GreaterThan(time));
            Assert.That(sandbox.Controller.movers[0].x, Is.GreaterThan(x), "the dash moves the platform along x");
            Assert.That(sandbox.Controller.movers[0].y, Is.GreaterThan(y), "the same dash moves the platform along y");
            yield return Tap(Key.Escape);
            yield return Tap(Key.Escape);
            yield return WaitFor(PhaseTitle.Scene);
            yield return Tap(Key.RightArrow); yield return Tap(Key.RightArrow); yield return Tap(Key.Z);
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
        var cap = PhaseKeys.Area(size, 4, 1).center;
        yield return Click(mouse, new Vector2(cap.x / size.x * Screen.width, (1f - cap.y / size.y) * Screen.height));
        Assert.That((keys.Capturing, keys.Row, keys.Column), Is.EqualTo((true, 4, 1)), "clicking a key cap captures that slot");
        LogAssert.NoUnexpectedReceived();
    }
}
