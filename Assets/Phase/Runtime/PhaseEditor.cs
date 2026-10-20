using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Phase
{
    // Paints terrain, spikes, lasers, crystals, platforms, gears and the spawn on the custom-map grid, then playtests it in place.
    public sealed class PhaseEditor : PhaseScreen
    {
        public const string Scene = "PhaseEditor", DraftKey = "phase.editor.v1";
        // One screen of cells is the grid window and the smallest room; a larger room scrolls under the same window.
        // The keyboard cursor scrolls to stay inside it; the cells the view shows just past it edit the same way.
        public const int Columns = 40, Rows = 22, MaxColumns = PhaseCustomMap.MaxWidth / 8, MaxRows = PhaseCustomMap.MaxHeight / 8, SizeStep = 8;
        // Cell tools: 0 empty, 1 terrain, 2–5 Up/Down/Left/Right spikes, 7–10 Up/Down/Left/Right lasers,
        // 11 jump crystal, 12 dash crystal. Spawn only moves the player start. A platform is one 24×8 start plus the
        // one-way line behind it; a gear is still body then end. Both stay outside the cell grid.
        // The panel holds one slot per type; R/E or the wheel turns spikes and lasers.
        public const int Erase = 0, Terrain = 1, SpikeTool = 2, Spawn = 6, LaserTool = 7, CrystalTool = 11, LastTool = 12, PlatformTool = 13, GearTool = 14;
        static readonly int[] kinds = { Terrain, SpikeTool, LaserTool, CrystalTool, LastTool, PlatformTool, GearTool, Spawn, Erase }, masks = { 7, 1, 2, 4, 5, 3, 6 },
            clockwise = { 0, 3, 1, 2 };
        // Panel slots: 9 types, 7 colors, then import, export, play and back, then narrower, wider, shorter and taller.
        public const int MaskSlot = 9, ImportSlot = 16, ExportSlot = 17, PlaySlot = 18, BackSlot = 19,
            NarrowerSlot = 20, WiderSlot = 21, ShorterSlot = 22, TallerSlot = 23, SlotCount = 24;
        const int PlatformWidth = 24;
        static readonly string[] spikeUp = { ".......", "...#...", "..###..", "..###..", ".#####.", ".#####.", "#######" },
            laserUp = { "...#...", "...#...", "...#...", ".#####.", ".#...#.", ".#.#.#.", ".#####." },
            spawnIcon = { "#......", "####...", "#####..", "####...", "#......", "#......", "#......" };
        // Indexed by cell tool code: erase, terrain, four spikes (Up, Down, Left, Right), spawn, four lasers, crystals, platform, gear.
        static readonly string[][] toolIcons = {
            new[] { "#.....#", ".#...#.", "..#.#..", "...#...", "..#.#..", ".#...#.", "#.....#" },
            Enumerable.Repeat("#######", 7).ToArray(), spikeUp, spikeUp.Reverse().ToArray(), Turn(spikeUp), Mirror(Turn(spikeUp)), spawnIcon,
            laserUp, laserUp.Reverse().ToArray(), Turn(laserUp), Mirror(Turn(laserUp)),
            new[] { "..###..", ".#####.", "##.####", "#######", "#######", ".#####.", "..###.." },
            new[] { ".......", ".#####.", ".#.###.", ".#####.", ".#####.", ".#####.", "......." },
            new[] { ".....#.", "#######", ".....#.", ".......", "#######", "#.#.#.#", "#######" },
            new[] { "...#...", ".#####.", ".##.##.", "##...##", ".##.##.", ".#####.", "...#..." }
        };
        static readonly string[] backIcon = { "...#...", "..#....", ".#.....", "#######", ".#.....", "..#....", "...#..." },
            exportIcon = { "....###....", "....###....", "....###....", ".#########.", "..#######..", "...#####...", "....###....", ".....#.....", "##.......##", "##.......##", "###########" },
            importIcon = { ".....#.....", "....###....", "...#####...", "..#######..", ".#########.", "....###....", "....###....", "....###....", "##.......##", "##.......##", "###########" },
            widerIcon = { ".......", ".#.#...", ".#.##..", ".#.###.", ".#.##..", ".#.#...", "......." };
        // The room's edge bar with an arrow: wider and taller push the right and bottom edges out, narrower and shorter pull them in.
        static readonly string[][] sizeIcons = { Mirror(widerIcon), widerIcon, Turn(widerIcon).Reverse().ToArray(), Turn(widerIcon) };
        byte[] tools = new byte[Columns * Rows], colors = new byte[Columns * Rows];
        int cols = Columns, rows = Rows;
        readonly List<DemoPlatform> platforms = new List<DemoPlatform>();
        readonly List<DemoGear> gears = new List<DemoGear>();
        // The placed origin while a platform line or a gear wall is still open.
        public Vector2Int? Anchor { get; private set; }
        DemoPlatform line;
        public int Tool { get; private set; } = Terrain;
        // The spike/laser direction index (Up, Down, Left, Right), kept across type changes.
        public int Facing { get; private set; }
        public int Mask { get; private set; } = 7;
        public int RoomColumns => cols;
        public int RoomRows => rows;
        // The room cell shown at the grid window's top-left corner.
        public Vector2Int Scroll { get; private set; }
        public Vector2Int Cursor { get; private set; } = new Vector2Int(8, 18);
        public Vector2Int SpawnCell { get; private set; } = new Vector2Int(3, 19);
        public PhaseSandbox Playtest { get; private set; }
        // The panel slot under the mouse.
        public int? Hover { get; private set; }
        protected override bool Hidden => Playtest != null;
        Vector2Int held, lastMouse = new Vector2Int(-1, -1), panScroll;
        // Where a pan began, while its button is held.
        Vector2Int? panFrom;
        float repeatAt, flash;
        // A mouse stroke paints or erases only when its press began on the grid.
        bool dirty, stroke;
        // A refused map whose spawn overlaps something: Wick's start and the flag slot blink with the border.
        public bool SpawnBlocked { get; private set; }

        static string[] Turn(string[] art) => Enumerable.Range(0, 7).Select(r => new string(Enumerable.Range(0, 7).Select(c => art[c][r]).ToArray())).ToArray();
        static string[] Mirror(string[] art) => art.Select(r => new string(r.Reverse().ToArray())).ToArray();
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void PhaseExportMap(string json);
        [DllImport("__Internal")] static extern void PhaseImportMap();
#endif
        protected override void Awake() { Load(); base.Awake(); }
        void Load()
        {
            string draft = PlayerPrefs.GetString(DraftKey, "");
            var parts = draft.Split(new[] { ';' }, 2);
            var spawn = parts[0].Split(',');
            // A larger room appends its columns and rows to the spawn cell; older drafts hold a 40×22 grid.
            int c = Columns, r = Rows;
            bool sized = spawn.Length == 4 && int.TryParse(spawn[2], out c) && int.TryParse(spawn[3], out r);
            if (parts.Length == 2 && (spawn.Length == 2 || sized) && c >= Columns && c <= MaxColumns && r >= Rows && r <= MaxRows && parts[1].Length >= c * r * 2 &&
                int.TryParse(spawn[0], out int sx) && int.TryParse(spawn[1], out int sy) && sx >= 0 && sx < c && sy >= 0 && sy <= r - 2)
            {
                Allocate(c, r);
                bool valid = true;
                for (int i = 0; i < tools.Length && valid; i++)
                {
                    int tool = parts[1][i * 2] - '0', mask = parts[1][i * 2 + 1] - '0';
                    valid = tool == Erase ? mask == 0 : tool > 0 && tool <= LastTool && tool != Spawn && mask >= 1 && mask <= 7;
                    tools[i] = (byte)tool; colors[i] = (byte)mask;
                }
                // Older drafts end after the cells; newer ones append their platforms and gears as JSON.
                string extra = parts[1].Substring(tools.Length * 2);
                try
                {
                    var mechanisms = extra.Length == 0 ? new PhaseDemoRoom() : PhaseCustomMap.ReadMechanisms(extra);
                    mechanisms.CreatePlatforms(); mechanisms.CreateGears();
                    platforms.AddRange(mechanisms.platforms ?? Array.Empty<DemoPlatform>()); gears.AddRange(mechanisms.gears ?? Array.Empty<DemoGear>());
                }
                catch (ArgumentException) { valid = false; }
                if (valid) { SpawnCell = new Vector2Int(sx, sy); return; }
            }
            Allocate(Columns, Rows); platforms.Clear(); gears.Clear();
            for (int x = 0; x < cols; x++) { tools[(rows - 1) * cols + x] = Terrain; colors[(rows - 1) * cols + x] = 7; }
        }
        void Allocate(int columns, int rowCount)
        {
            cols = columns; rows = rowCount; tools = new byte[cols * rows]; colors = new byte[cols * rows];
            Scroll = Vector2Int.zero; scene = null;
        }
        void Save()
        {
            var cells = new char[tools.Length * 2];
            for (int i = 0; i < tools.Length; i++) { cells[i * 2] = (char)('0' + tools[i]); cells[i * 2 + 1] = (char)('0' + colors[i]); }
            string size = cols == Columns && rows == Rows ? "" : $",{cols},{rows}";
            PlayerPrefs.SetString(DraftKey, $"{SpawnCell.x},{SpawnCell.y}{size};{new string(cells)}{PhaseCustomMap.MechanismJson(Room())}");
            PlayerPrefs.Save(); dirty = false;
        }
        // The map as authored; a nonzero down shifts everything below the room's top, which the scrolling preview uses.
        public PhaseDemoRoom Room(int down = 0)
        {
            var blocks = new List<Block>(); var spikes = new List<DemoSpike>(); var lasers = new List<DemoLaser>(); var crystals = new List<DemoCrystal>();
            for (int i = 0; i < tools.Length; i++)
            {
                int x = i % cols * 8, y = i / cols * 8 + down, tool = tools[i];
                if (tool == Terrain) blocks.Add(new Block(x, y, 8, 8, -1, colors[i]));
                else if (tool >= CrystalTool) crystals.Add(new DemoCrystal { x = x, y = y, kind = (CrystalKind)(tool - CrystalTool), phaseMask = colors[i] });
                else if (tool >= LaserTool) lasers.Add(new DemoLaser { x = x, y = y, direction = (LaserDirection)(tool - LaserTool), phaseMask = colors[i] });
                else if (tool != Erase) spikes.Add(new DemoSpike { x = x, y = y, length = 8, direction = (SpikeDirection)(tool - 2), phaseMask = colors[i] });
            }
            return new PhaseDemoRoom { name = "自訂地圖", width = cols * 8, height = rows * 8, blocks = blocks.ToArray(), spikes = spikes.ToArray(), lasers = lasers.ToArray(), crystals = crystals.ToArray(),
                platforms = platforms.Select(p => Down(p, down)).ToArray(), gears = gears.Select(g => Down(g, down)).ToArray(), spawnX = SpawnCell.x * 8, spawnY = SpawnCell.y * 8 + down };
        }
        static DemoPlatform Down(DemoPlatform p, int down) => down == 0 ? p : new DemoPlatform { w = p.w, h = p.h, nodeX = p.nodeX, nodeY = p.nodeY.Select(y => y + down).ToArray(), returns = p.returns, phaseMask = p.phaseMask };
        static DemoGear Down(DemoGear g, int down) => down == 0 ? g : new DemoGear { wall = new Block(g.wall.x, g.wall.y + down, g.wall.w, g.wall.h), platform = new Block(g.platform.x, g.platform.y + down, g.platform.w, g.platform.h),
            top = g.top + down, endX = g.endX, phaseMask = g.phaseMask };
        static bool IsMechanism(int tool) => tool >= PlatformTool;
        static RectInt Box(Block b) => new RectInt((int)b.x, (int)b.y, (int)b.w, (int)b.h);
        static RectInt Box(DemoPlatform p) => new RectInt((int)p.nodeX[0], (int)p.nodeY[0], (int)p.w, (int)p.h);
        static RectInt Box(Vector2Int cell, int w = 8, int h = 8) => new RectInt(cell.x * 8, cell.y * 8, w, h);
        IEnumerable<RectInt> Bodies() => platforms.Select(Box).Concat(gears.SelectMany(g => new[] { Box(g.wall), Box(g.platform) }));
        bool Free(RectInt area)
        {
            if (area.xMin < 0 || area.yMin < 0 || area.xMax > cols * 8 || area.yMax > rows * 8 || Bodies().Any(b => b.Overlaps(area))) return false;
            for (int y = area.yMin / 8; y < area.yMax / 8; y++) for (int x = area.xMin / 8; x < area.xMax / 8; x++) if (tools[y * cols + x] != Erase) return false;
            return true;
        }
        // The 24×8 body stays inside the map. Stops keep both axes, so a segment may be diagonal.
        Vector2Int Fit(Vector2Int cell) { cell.x = Math.Min(cell.x, cols - PlatformWidth / 8); return cell; }
        static RectInt Stop(DemoPlatform platform, int i) => new RectInt((int)platform.nodeX[i], (int)platform.nodeY[i], (int)platform.w, (int)platform.h);
        static void AddStop(DemoPlatform platform, Vector2Int cell)
        {
            int n = platform.nodeX.Length;
            Array.Resize(ref platform.nodeX, n + 1); Array.Resize(ref platform.nodeY, n + 1);
            platform.nodeX[n] = cell.x * 8; platform.nodeY[n] = cell.y * 8;
        }
        // An 8×56 wall at the anchor drives a 56×8 bridge two cells to its right on the chosen row. The end column sets
        // equal wall and bridge travel of 8–24 px, which keeps the bridge rack over the output pinion.
        DemoGear GearAt(Vector2Int wall, Vector2Int cell)
        {
            int x = wall.x * 8, y = wall.y * 8, startX = x + 16;
            int travel = Mathf.Clamp(cell.x * 8 - startX, 8, Math.Min(24, Math.Min(y, cols * 8 - 56 - startX)));
            return new DemoGear { wall = new Block(x, y, 8, 56), top = y - travel, platform = new Block(startX, cell.y * 8, 56, 8), endX = startX + travel };
        }
        // The first press places the platform origin or the gear wall. Later presses extend the one-way line, or set the gear's end.
        bool Place(Vector2Int cell)
        {
            if (Tool == GearTool)
            {
                if (Anchor is Vector2Int wall)
                {
                    var gear = GearAt(wall, cell);
                    if (!Free(Box(gear.platform))) { flash = .4f; return true; }
                    gears.Add(gear); Anchor = null; return Edited();
                }
                if (cell.y >= 1 && cell.x * 8 + 80 <= cols * 8 && Free(Box(cell, 8, 56))) Anchor = cell; else flash = .4f;
                return true;
            }
            cell = Fit(cell);
            if (Anchor is Vector2Int start)
            {
                if (line == null)
                {
                    if (cell == start) return false;
                    line = new DemoPlatform { nodeX = new double[] { start.x * 8, cell.x * 8 }, nodeY = new double[] { start.y * 8, cell.y * 8 } };
                    platforms.Add(line);
                }
                else
                {
                    int last = line.nodeX.Length - 1;
                    if (line.nodeX[last] == cell.x * 8 && line.nodeY[last] == cell.y * 8) return false;
                    AddStop(line, cell);
                }
                return Edited();
            }
            if (!Free(Box(cell, PlatformWidth))) { flash = .4f; return true; }
            Anchor = cell; line = null; return true;
        }
        // Content changed: the draft needs saving and the preview scene a rebuild.
        bool Edited() { dirty = true; scene = null; return true; }
        bool Cancel()
        {
            if (Anchor == null) return false;
            Anchor = null; line = null; return true;
        }
        static int KindOf(int tool) => tool >= SpikeTool && tool < Spawn ? SpikeTool : tool >= LaserTool && tool < CrystalTool ? LaserTool : tool;
        static bool Turns(int kind) => kind == SpikeTool || kind == LaserTool;
        int ToolFor(int kind) => Turns(kind) ? kind + Facing : kind;
        void Select(int kind) { Tool = ToolFor(kind); Anchor = null; line = null; }
        // One clockwise (1) or counter-clockwise (-1) turn; other types ignore it.
        bool Rotate(int turn)
        {
            if (!Turns(KindOf(Tool))) return false;
            Facing = clockwise[(Array.IndexOf(clockwise, Facing) + turn + 4) % 4];
            Tool = ToolFor(KindOf(Tool)); return true;
        }
        bool Apply(Vector2Int cell, int tool)
        {
            if (tool == Spawn)
            {
                var spawn = new Vector2Int(cell.x, Math.Min(cell.y, rows - 2));
                if (spawn == SpawnCell) return false;
                SpawnCell = spawn; return Edited();
            }
            var area = Box(cell);
            if (tool == Erase && platforms.RemoveAll(p => Box(p).Overlaps(area)) + gears.RemoveAll(g => Box(g.wall).Overlaps(area) || Box(g.platform).Overlaps(area)) > 0)
            {
                if (line != null && !platforms.Contains(line)) { line = null; Anchor = null; }
                return Edited();
            }
            if (tool != Erase && Bodies().Any(b => b.Overlaps(area))) return false;
            int i = cell.y * cols + cell.x; byte color = (byte)(tool == Erase ? 0 : Mask);
            if (tools[i] == tool && colors[i] == color) return false;
            tools[i] = (byte)tool; colors[i] = color; return Edited();
        }

        // A fixed panel of 2× icons sits right of the grid, or below it on tall screens, and never covers a cell.
        const int Pitch = 18, Gap = 6, Pad = 4, SideW = Pad * 2 + 4 * Pitch, SideH = Pad * 2 + 7 * Pitch + 3 * Gap,
            BelowTop = Rows * 8 + 4, BelowW = Pad * 2 + 15 * Pitch + 2 * Gap, BelowH = Pad * 2 + 2 * Pitch + Gap;
        // The view keeps the screen's aspect and grows until the grid and panel fit; the layout with the larger grid wins.
        public static Vector2Int ViewFor(int width, int height)
        {
            double w = Math.Max(1, width), h = Math.Max(1, height);
            Vector2Int Fit(int minW, int minH) => new Vector2Int(Math.Max(minW, (int)Math.Floor(minH * w / h + .5)), Math.Max(minH, (int)Math.Ceiling(minW * h / w)));
            // PhasePixels centers the 180-pixel play band, so the panel below the grid needs that much room under the band too.
            Vector2Int side = Fit(Columns * 8 + SideW, 180), below = Fit(Columns * 8, 2 * (BelowTop + BelowH) - 180);
            return side.x <= below.x ? side : below;
        }
        static bool Side(Vector2Int view) => view.x >= Columns * 8 + SideW;
        // The grid's top row in the frame, as PhasePixels.OriginY places it.
        static int GridTop(Vector2Int view) => -((180 - view.y) / 2);
        public static RectInt PanelIn(Vector2Int view) => Side(view)
            ? new RectInt(Columns * 8, GridTop(view) + (Rows * 8 - SideH) / 2, SideW, SideH)
            : new RectInt((Columns * 8 - BelowW) / 2, GridTop(view) + BelowTop, BelowW, BelowH);
        // The side panel stacks tools, colors, files/play/back and the size arrows in four columns; below the grid the tools
        // fill one row and the colors share the next with files/play/back and the size arrows.
        public static RectInt SlotIn(Vector2Int view, int slot)
        {
            var panel = PanelIn(view);
            int group = slot < MaskSlot ? 0 : slot < ImportSlot ? 1 : slot < NarrowerSlot ? 2 : 3, i = slot - new[] { 0, MaskSlot, ImportSlot, NarrowerSlot }[group];
            var cell = Side(view) ? new Vector2Int(i % 4 * Pitch, (new[] { 0, 3, 5, 6 }[group] + i / 4) * Pitch + group * Gap)
                : new Vector2Int((new[] { 0, 0, 7, 11 }[group] + i) * Pitch + Math.Max(0, group - 1) * Gap, group == 0 ? 0 : Pitch + Gap);
            return new RectInt(panel.x + Pad + cell.x, panel.y + Pad + cell.y, Pitch, Pitch);
        }
        protected override Vector2Int ViewSize(int width, int height) => ViewFor(width, height);
        Vector2Int View => new Vector2Int(pixels.Width, pixels.Height);
        // Room pixel under the frame's top-left corner: the scroll plus the band PhasePixels centers vertically.
        Vector2Int Origin => new Vector2Int(Scroll.x * 8, Scroll.y * 8 + (180 - View.y) / 2);
        int MaxScrollX => cols - Columns;
        int MaxScrollY => rows - Rows;
        PhaseController scene;
        Vector2Int sceneView;
        // A larger room is drawn by PhasePixels' own scrolling camera, so the controller pads the room: to the right by the
        // panel's width, and above and below by the band the centered view shows, which keeps every scroll position inside the
        // camera's clamp. The room's content moves down by that pad.
        int Lift => rows > Rows ? (View.y - 176 - (View.y - 180) / 2 + 3) / 4 * 4 : 0;
        PhaseController Preview()
        {
            if (scene != null && sceneView == View) return scene;
            var room = Room(Lift);
            int width = cols > Columns ? cols * 8 + (Math.Max(0, View.x - Columns * 8) + 7) / 8 * 8 : 320, height = rows > Rows ? rows * 8 + 2 * Lift : 180;
            sceneView = View;
            return scene = new PhaseController(room.blocks, room.spawnX, room.spawnY, room.CreatePlatforms(), room.CreateGears(), room.CreateSpikes(), room.CreateLasers(), room.CreateCrystals(), width, height);
        }
        int? Slot(Vector2Int p)
        {
            for (int slot = 0; slot < SlotCount; slot++) if (SlotIn(View, slot).Contains(p)) return slot;
            return null;
        }
        protected override void Render()
        {
            // A one-screen room keeps its 320-pixel width, so the floor stops at the grid's edge.
            var origin = Origin; int ox = origin.x, oy = origin.y;
            pixels.Scroll(ox, oy + Lift);
            pixels.Draw(Preview());
            var frame = new PixelFrame(pixels.Texture);
            Color32 color = PhasePixels.SurfaceColor(Mask);
            int cx = Cursor.x * 8 - ox, cy = Cursor.y * 8 - oy;
            void Body(RectInt b, float amount) => frame.Outline(b.x - ox - 1, b.y - oy - 1, b.width + 2, b.height + 2, PixelFrame.Light, amount);
            void Route(RectInt from, RectInt to, float amount)
            {
                int ax = (int)from.center.x - ox, ay = (int)from.center.y - oy, bx = (int)to.center.x - ox, by = (int)to.center.y - oy, n = Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay));
                for (int i = 0; i <= n; i += 2) frame.Set(ax + (bx - ax) * i / Math.Max(n, 1), ay + (by - ay) * i / Math.Max(n, 1), PixelFrame.Light, amount);
            }
            void Gear(DemoGear g, float amount)
            {
                var start = Box(g.platform); var end = new RectInt((int)g.endX, start.y, start.width, start.height);
                Body(new RectInt((int)g.wall.x, (int)g.top, (int)g.wall.w, (int)g.wall.h), amount); Body(end, amount); Route(start, end, amount);
            }
            // Dotted segments follow the clicked stops, including a diagonal.
            foreach (var p in platforms)
                for (int i = 1; i < p.nodeX.Length; i++) { Route(Stop(p, i - 1), Stop(p, i), .5f); Body(Stop(p, i), .35f); }
            foreach (var g in gears) Gear(g, .35f);
            if (Anchor is Vector2Int anchor)
            {
                if (Tool == GearTool) { var g = GearAt(anchor, Cursor); Body(Box(g.wall), 1); Body(Box(g.platform), 1); Gear(g, .8f); }
                else
                {
                    var body = Box(anchor, PlatformWidth); Body(body, 1);
                    var from = line == null ? body : Stop(line, line.nodeX.Length - 1);
                    var next = Box(Fit(Cursor), PlatformWidth);
                    if (next.x != from.x || next.y != from.y) { Route(from, next, .8f); Body(next, .8f); }
                }
            }
            else if (IsMechanism(Tool)) Body(Tool == GearTool ? Box(Cursor, 8, 56) : Box(Fit(Cursor), PlatformWidth), .6f);
            if (Tool != Erase && Tool != Spawn && !IsMechanism(Tool)) frame.Fill(cx, cy, 8, 8, color, .35f);
            if (Turns(KindOf(Tool))) frame.Blit(toolIcons[Tool], cx, cy, color, 1, .9f);
            frame.Outline(cx - 1, cy - 1, 10, 10, PixelFrame.Light, .9f);
            // Beyond a larger room's edges the padding is dimmed, and thin bars along the grid's right and bottom edges show where the view sits in the room.
            int right = cols * 8 - ox, bottom = rows * 8 - oy;
            var panel = PanelIn(View);
            var area = Side(View) ? new RectInt(0, 0, panel.x, frame.Height) : new RectInt(0, 0, frame.Width, panel.y);
            if (rows > Rows) { frame.Fill(0, 0, frame.Width, Math.Max(0, -oy), PixelFrame.Ink, .85f); frame.Fill(0, bottom, frame.Width, Math.Max(0, frame.Height - bottom), PixelFrame.Ink, .85f); }
            if (cols > Columns) frame.Fill(right, 0, Math.Max(0, frame.Width - right), frame.Height, PixelFrame.Ink, .85f);
            if (cols > Columns)
            {
                int span = area.width, length = Math.Max(8, span * Columns / cols);
                frame.Fill(area.x, area.yMax - 3, span, 2, PixelFrame.Ink, .6f);
                frame.Fill(area.x + (span - length) * Scroll.x / MaxScrollX, area.yMax - 3, length, 2, PixelFrame.Light, .8f);
            }
            if (rows > Rows)
            {
                int span = area.height, length = Math.Max(8, span * Rows / rows);
                frame.Fill(area.xMax - 3, area.y, 2, span, PixelFrame.Ink, .6f);
                frame.Fill(area.xMax - 3, area.y + (span - length) * Scroll.y / MaxScrollY, 2, length, PixelFrame.Light, .8f);
            }
            // The opaque panel hides the cursor outline and previews that reach past the grid's edge.
            frame.Fill(panel.x, panel.y, panel.width, panel.height, PixelFrame.Ink);
            for (int slot = 0; slot < SlotCount; slot++)
            {
                var cell = SlotIn(View, slot); int x = cell.x + 2, y = cell.y + 2;
                bool selected = slot < MaskSlot ? kinds[slot] == KindOf(Tool) : slot < ImportSlot && masks[slot - MaskSlot] == Mask;
                if (slot == Hover) frame.Fill(cell.x, cell.y, Pitch, Pitch, PixelFrame.Light, .2f);
                if (slot < MaskSlot) frame.Blit(toolIcons[ToolFor(kinds[slot])], x, y, kinds[slot] == Spawn || kinds[slot] == Erase || IsMechanism(kinds[slot]) ? PixelFrame.Light : color, 2, selected ? 1 : .45f);
                else if (slot < ImportSlot) frame.Fill(x, y, 14, 14, PhasePixels.SurfaceColor(masks[slot - MaskSlot]), selected ? 1 : .6f);
                else if (slot < BackSlot) frame.Blit(slot == ImportSlot ? importIcon : slot == ExportSlot ? exportIcon : PhaseMenu.PlayIcon, cell.x + 4, cell.y + 4, PixelFrame.Light);
                else if (slot == BackSlot) frame.Blit(backIcon, x, y, PixelFrame.Light, 2);
                else frame.Blit(sizeIcons[slot - NarrowerSlot], x, y, PixelFrame.Light, 2, Sized(slot) != new Vector2Int(cols, rows) ? 1 : .3f);
                if (selected) frame.Outline(cell.x, cell.y, Pitch, Pitch, PixelFrame.Light);
            }
            if (flash > 0) frame.Warn();
            if (SpawnBlocked && (int)(flash * 6) % 2 == 0)
            {
                var wick = new Player(SpawnCell.x * 8, SpawnCell.y * 8);
                frame.Fill((int)wick.x - ox, (int)wick.y - oy, (int)wick.w, (int)wick.h, PixelFrame.Warning, .5f);
                frame.Outline((int)wick.x - ox - 1, (int)wick.y - oy - 1, (int)wick.w + 2, (int)wick.h + 2, PixelFrame.Warning);
                var flag = SlotIn(View, Array.IndexOf(kinds, Spawn));
                frame.Outline(flag.x, flag.y, Pitch, Pitch, PixelFrame.Warning); frame.Outline(flag.x + 1, flag.y + 1, Pitch - 2, Pitch - 2, PixelFrame.Warning);
            }
            frame.Apply();
        }
        void Choose(int slot)
        {
            if (slot < MaskSlot) Select(kinds[slot]);
            else if (slot < ImportSlot) Mask = masks[slot - MaskSlot];
            else if (slot == ImportSlot) RequestImport();
            else if (slot == ExportSlot) ExportDraft();
            else if (slot == PlaySlot) { StartPlaytest(); return; }
            else if (slot == BackSlot) { Leave(); return; }
            else { var size = Sized(slot); ResizeTo(size.x, size.y); }
            Refresh();
        }
        // The room size a size slot asks for, held between one screen and the largest map.
        Vector2Int Sized(int slot) => new Vector2Int(
            Math.Max(Columns, Math.Min(MaxColumns, cols + (slot == WiderSlot ? SizeStep : slot == NarrowerSlot ? -SizeStep : 0))),
            Math.Max(Rows, Math.Min(MaxRows, rows + (slot == TallerSlot ? SizeStep : slot == ShorterSlot ? -SizeStep : 0))));
        static bool Inside(RectInt room, RectInt b) => b.xMin >= 0 && b.yMin >= 0 && b.xMax <= room.xMax && b.yMax <= room.yMax;
        // A smaller room must still hold every cell, the spawn and each mechanism stop, so shrinking never deletes anything.
        bool Holds(int columns, int rowCount)
        {
            for (int i = 0; i < tools.Length; i++) if (tools[i] != Erase && (i % cols >= columns || i / cols >= rowCount)) return false;
            if (SpawnCell.x >= columns || SpawnCell.y > rowCount - 2) return false;
            var room = new RectInt(0, 0, columns * 8, rowCount * 8);
            foreach (var p in platforms) for (int i = 0; i < p.nodeX.Length; i++) if (!Inside(room, Stop(p, i))) return false;
            foreach (var g in gears)
                if (!Inside(room, Box(g.wall)) || !Inside(room, Box(g.platform)) || !Inside(room, new RectInt((int)g.wall.x, (int)g.top, (int)g.wall.w, (int)g.wall.h)) ||
                    !Inside(room, new RectInt((int)g.endX, (int)g.platform.y, (int)g.platform.w, (int)g.platform.h))) return false;
            return true;
        }
        // Grows or shrinks the room at its right and bottom edges; a shrink that would cut something flashes and changes nothing.
        public bool ResizeTo(int columns, int rowCount)
        {
            columns = Math.Max(Columns, Math.Min(MaxColumns, columns)); rowCount = Math.Max(Rows, Math.Min(MaxRows, rowCount));
            if (columns == cols && rowCount == rows) return false;
            if (!Holds(columns, rowCount)) { flash = .4f; return false; }
            var oldTools = tools; var oldColors = colors; int oldCols = cols;
            cols = columns; rows = rowCount; tools = new byte[cols * rows]; colors = new byte[cols * rows];
            for (int i = 0; i < oldTools.Length; i++)
                if (i % oldCols < cols && i / oldCols < rows) { tools[i / oldCols * cols + i % oldCols] = oldTools[i]; colors[i / oldCols * cols + i % oldCols] = oldColors[i]; }
            Anchor = null; line = null; Edited();
            Cursor = new Vector2Int(Math.Min(Cursor.x, cols - 1), Math.Min(Cursor.y, rows - 1)); ScrollTo(Scroll.x, Scroll.y);
            Save(); return true;
        }
        bool ScrollTo(int x, int y)
        {
            var next = new Vector2Int(Math.Max(0, Math.Min(MaxScrollX, x)), Math.Max(0, Math.Min(MaxScrollY, y)));
            if (next == Scroll) return false;
            Scroll = next; return true;
        }
        // Scrolls just far enough that the cell sits at least `margin` cells inside the window.
        bool Reveal(Vector2Int cell, int margin = 2) => ScrollTo(
            Math.Max(Math.Min(Scroll.x, cell.x - margin), cell.x + margin + 1 - Columns), Math.Max(Math.Min(Scroll.y, cell.y - margin), cell.y + margin + 1 - Rows));
        // Every spawn refusal in PhaseCustomMap.Validate names 出生點 (the spawn).
        void Refuse(ArgumentException error)
        {
            flash = 1.2f; SpawnBlocked = error.Message.Contains("出生點");
            if (SpawnBlocked) Reveal(SpawnCell, 4);
            Refresh();
        }
        void Leave() { if (dirty) Save(); Open(PhaseTitle.Scene); }
        protected override void Tick(Keyboard k, Mouse m)
        {
            if (Playtest != null)
            {
                if (k != null && k.escapeKey.wasPressedThisFrame) StopPlaytest();
                return;
            }
            bool changed = false, painting = false;
            if (flash > 0) { flash -= Time.unscaledDeltaTime; SpawnBlocked &= flash > 0; changed = true; }
            if (k != null)
            {
                if (k.escapeKey.wasPressedThisFrame) { if (!Cancel()) { Leave(); return; } changed = true; }
                if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) { StartPlaytest(); return; }
                for (int i = 0; i < masks.Length; i++) if (k[Key.Digit1 + i].wasPressedThisFrame) { Mask = masks[i]; changed = true; }
                // R/E turn the tool even when bound to an action; that action waits until they are released.
                if (k.rKey.wasPressedThisFrame) changed |= Rotate(1);
                if (k.eKey.wasPressedThisFrame) changed |= Rotate(-1);
                bool turning = k.rKey.isPressed || k.eKey.isPressed;
                if (!turning && PhaseSandbox.Pressed(k, 5, true)) { Select(kinds[(Array.IndexOf(kinds, KindOf(Tool)) + 1) % kinds.Length]); changed = true; }
                var input = turning ? default : PhaseSandbox.ReadControls(k);
                var step = new Vector2Int(input.x, input.y);
                if (step != Vector2Int.zero && (step != held || Time.unscaledTime >= repeatAt))
                {
                    repeatAt = Time.unscaledTime + (step != held ? .25f : .06f);
                    Cursor = new Vector2Int(Math.Max(0, Math.Min(cols - 1, Cursor.x + step.x)), Math.Max(0, Math.Min(rows - 1, Cursor.y + step.y)));
                    Reveal(Cursor); changed = true;
                }
                held = step;
                painting = input.jumpHeld || input.grab;
                if (IsMechanism(Tool) && input.jumpHeld) { if (PhaseSandbox.Pressed(k, 4, true)) changed |= Place(Cursor); }
                else if (input.jumpHeld) changed |= Apply(Cursor, Tool);
                else if (input.grab) changed |= Cancel() || Apply(Cursor, Erase);
            }
            if (m != null)
            {
                var p = FramePoint(m); var slot = Slot(p); var origin = Origin;
                int x = (p.x + origin.x) >> 3, y = (p.y + origin.y) >> 3;
                bool onGrid = !PanelIn(View).Contains(p) && x >= 0 && x < cols && y >= 0 && y < rows;
                if (slot != Hover) { Hover = slot; changed = true; }
                // A larger room scrolls by the wheel (Ctrl or a sideways wheel for columns) and by dragging with the middle
                // button, or the left one under Ctrl. A room that fits vertically keeps the wheel for turning spikes and lasers.
                bool ctrl = k != null && (k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed), pan = m.middleButton.wasPressedThisFrame || (ctrl && m.leftButton.wasPressedThisFrame);
                var wheel = m.scroll.ReadValue();
                if (wheel != Vector2.zero)
                {
                    int sideways = wheel.x != 0 ? Math.Sign(wheel.x) : ctrl ? -Math.Sign(wheel.y) : 0;
                    if (sideways != 0) changed |= ScrollTo(Scroll.x + sideways * 2, Scroll.y);
                    else if (rows > Rows) changed |= ScrollTo(Scroll.x, Scroll.y - Math.Sign(wheel.y) * 2);
                    else changed |= Rotate(wheel.y < 0 ? 1 : -1);
                    lastMouse = new Vector2Int(-1, -1);
                }
                if (pan && onGrid) { panFrom = p; panScroll = Scroll; }
                if (panFrom is Vector2Int grip)
                {
                    if (m.middleButton.isPressed || m.leftButton.isPressed)
                    {
                        changed |= ScrollTo(panScroll.x - Mathf.RoundToInt((p.x - grip.x) / 8f), panScroll.y - Mathf.RoundToInt((p.y - grip.y) / 8f));
                        lastMouse = new Vector2Int(-1, -1);
                    }
                    else panFrom = null;
                }
                if (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame) stroke = onGrid && !pan;
                if (m.leftButton.wasPressedThisFrame && slot is int chosen) { Choose(chosen); return; }
                if (onGrid && p != lastMouse) { Cursor = new Vector2Int(x, y); changed = true; }
                lastMouse = p;
                if (onGrid && stroke && m.leftButton.isPressed)
                {
                    if (!IsMechanism(Tool)) { painting = true; changed |= Apply(Cursor, Tool); }
                    else if (m.leftButton.wasPressedThisFrame) changed |= Place(Cursor);
                }
                else if (onGrid && stroke && m.rightButton.isPressed) { painting = true; changed |= Cancel() || Apply(Cursor, Erase); }
            }
            if (dirty && !painting) Save();
            if (changed) Refresh();
        }
        // Web saves and opens JSON files through the page; elsewhere the system clipboard carries the same JSON.
        public bool ExportDraft()
        {
            var room = Room();
            try { PhaseCustomMap.Validate(room); }
            catch (ArgumentException error) { Refuse(error); return false; }
            string json = PhaseCustomMap.ToJson(room);
#if UNITY_WEBGL && !UNITY_EDITOR
            PhaseExportMap(json);
#else
            GUIUtility.systemCopyBuffer = json;
#endif
            return true;
        }
        void RequestImport()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PhaseImportMap();
#else
            ImportDraft(GUIUtility.systemCopyBuffer);
#endif
        }
        // Replaces the draft with a custom-map JSON file; an invalid file flashes and leaves the draft unchanged.
        public void ImportDraft(string json)
        {
            PhaseDemoRoom room;
            try { room = PhaseCustomMap.Parse(json); }
            catch (ArgumentException) { flash = .4f; Refresh(); return; }
            Anchor = null; line = null; Allocate(room.GridWidth / 8, room.GridHeight / 8);
            Cursor = new Vector2Int(Math.Min(Cursor.x, cols - 1), Math.Min(Cursor.y, rows - 1));
            platforms.Clear(); platforms.AddRange(room.platforms ?? Array.Empty<DemoPlatform>());
            gears.Clear(); gears.AddRange(room.gears ?? Array.Empty<DemoGear>());
            void Put(double x, double y, double w, double h, int tool, int mask)
            {
                for (int cy = (int)y / 8; cy < (y + h) / 8; cy++)
                    for (int cx = (int)x / 8; cx < (x + w) / 8; cx++) { tools[cy * cols + cx] = (byte)tool; colors[cy * cols + cx] = (byte)mask; }
            }
            foreach (var block in room.blocks) Put(block.x, block.y, block.w, block.h, Terrain, block.Mask);
            foreach (var spike in room.CreateSpikes()) Put(spike.x, spike.y, spike.w, spike.h, 2 + (int)spike.direction, spike.Mask);
            foreach (var laser in room.lasers ?? Array.Empty<DemoLaser>()) Put(laser.x, laser.y, 8, 8, LaserTool + (int)laser.direction, laser.phaseMask);
            foreach (var crystal in room.crystals ?? Array.Empty<DemoCrystal>()) Put(crystal.x, crystal.y, 8, 8, CrystalTool + (int)crystal.kind, crystal.phaseMask);
            SpawnCell = new Vector2Int((int)room.spawnX / 8, (int)room.spawnY / 8);
            Save(); Refresh();
        }
        public bool StartPlaytest()
        {
            Anchor = null; line = null;
            var room = Room();
            try { PhaseCustomMap.Validate(room); }
            catch (ArgumentException error) { Refuse(error); return false; }
            Play(room); return true;
        }
        void Play(PhaseDemoRoom room)
        {
            if (dirty) Save();
            surface.enabled = false;
            try { Playtest = PhaseSandbox.CreateCustom(room, pixelCamera, pixelMaterial, transform); }
            catch { surface.enabled = true; throw; }
        }
        public void StopPlaytest()
        {
            if (Playtest == null) return;
            Playtest.gameObject.SetActive(false); Destroy(Playtest.gameObject); Playtest = null;
            surface.enabled = true; Refresh(true);
        }
        // A bridge LoadCustomMap sent while editing plays here too and returns to this draft unchanged.
        public override void LoadCustomMap(string json)
        {
            if (leaving) { PhaseSandbox.MapResult(false, PhaseSandbox.Busy); return; }
            try
            {
                var room = PhaseCustomMap.Parse(json);
                StopPlaytest(); Play(room);
                PhaseSandbox.Pause("false");
                PhaseSandbox.MapResult(true);
            }
            catch (Exception error) { PhaseSandbox.MapResult(false, error.Message); }
        }
    }
}
