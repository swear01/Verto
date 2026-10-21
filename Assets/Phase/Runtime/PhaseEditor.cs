using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Phase
{
    // Paints terrain, spikes, lasers, jump crystals, jump platforms and the spawn on the custom-map grid, then playtests it in place.
    public sealed class PhaseEditor : PhaseScreen
    {
        public const string Scene = "PhaseEditor", DraftKey = "phase.editor.v1";
        // One screen of cells is the grid window and the smallest room; a larger room scrolls under the same window.
        // The keyboard cursor scrolls to stay inside it; the cells the view shows just past it edit the same way.
        public const int Columns = 40, Rows = 22, MaxColumns = PhaseCustomMap.MaxWidth / 8, MaxRows = PhaseCustomMap.MaxHeight / 8, SizeStep = 8;
        // Cell tools: 0 empty, 1 terrain, 2–5 Up/Down/Left/Right spikes, 7–10 Up/Down/Left/Right lasers, 11 jump crystal, 12 spike-covered terrain.
        // Spawn only moves the player start; PlatformTool and CoverTool store nothing in a cell (platforms are kept apart, CoverTool
        // paints or toggles cell 12 and platform covers). The panel holds one slot per type; R/E or the wheel turns spikes and lasers.
        public const int Erase = 0, Terrain = 1, SpikeTool = 2, Spawn = 6, LaserTool = 7, CrystalTool = 11, Covered = 12, PlatformTool = 13, CoverTool = 14, LastTool = 11;
        static readonly int[] kinds = { Terrain, SpikeTool, LaserTool, CrystalTool, PlatformTool, CoverTool, Spawn, Erase }, masks = { 7, 1, 2, 4, 5, 3, 6 },
            clockwise = { 0, 3, 1, 2 };
        // Panel slots: 8 types, 7 colors, then import, export, play and back, then narrower, wider, shorter and taller.
        public const int MaskSlot = 8, ImportSlot = 15, ExportSlot = 16, PlaySlot = 17, BackSlot = 18,
            NarrowerSlot = 19, WiderSlot = 20, ShorterSlot = 21, TallerSlot = 22, SlotCount = 23;
        static readonly string[] spikeUp = { ".......", "...#...", "..###..", "..###..", ".#####.", ".#####.", "#######" },
            laserUp = { "...#...", "...#...", "...#...", ".#####.", ".#...#.", ".#.#.#.", ".#####." },
            spawnIcon = { "#......", "####...", "#####..", "####...", "#......", "#......", "#......" },
            platformIcon = { ".......", "#######", "#######", ".......", "..###..", "...#...", "......." },
            coverIcon = { "..#.#..", ".#####.", "#######", ".#####.", "#######", ".#####.", "..#.#.." };
        // Indexed by tool code: erase, terrain, four spikes (Up, Down, Left, Right), spawn, four lasers, crystal, covered terrain, platform, cover.
        static readonly string[][] toolIcons = {
            new[] { "#.....#", ".#...#.", "..#.#..", "...#...", "..#.#..", ".#...#.", "#.....#" },
            Enumerable.Repeat("#######", 7).ToArray(), spikeUp, spikeUp.Reverse().ToArray(), Turn(spikeUp), Mirror(Turn(spikeUp)), spawnIcon,
            laserUp, laserUp.Reverse().ToArray(), Turn(laserUp), Mirror(Turn(laserUp)),
            new[] { "..###..", ".#####.", "##.####", "#######", "#######", ".#####.", "..###.." },
            coverIcon, platformIcon, coverIcon
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
        // The platform being placed: the centre cells clicked so far, and once the second click set it, its orientation and length.
        readonly List<Vector2Int> draft = new List<Vector2Int>();
        bool draftVertical, coverOn;
        int draftLength;
        public bool Placing => draft.Count > 0;
        // 0 until the second click gives the platform being placed its length.
        public int DraftLength => draftLength;
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
            string saved = PlayerPrefs.GetString(DraftKey, "");
            // The draft is the map file's own JSON, so older files migrate the same way; drafts written before it hold one tool/mask pair per cell.
            if (saved.StartsWith("{"))
            {
                try { Fill(PhaseCustomMap.ParseRoom(saved, false)); return; }
                catch (ArgumentException) { }
            }
            var parts = saved.Split(new[] { ';' }, 2);
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
                    // Older drafts may hold a dash crystal (12), which is gone; its cell is left empty.
                    if (tool == 12) tool = mask = Erase;
                    valid = tool == Erase ? mask == 0 : tool > 0 && tool <= LastTool && tool != Spawn && mask >= 1 && mask <= 7;
                    tools[i] = (byte)tool; colors[i] = (byte)mask;
                }
                // Older drafts may append dash platforms and gears after the cells; they are dropped.
                if (valid) { SpawnCell = new Vector2Int(sx, sy); return; }
            }
            Allocate(Columns, Rows);
            for (int x = 0; x < cols; x++) { tools[(rows - 1) * cols + x] = Terrain; colors[(rows - 1) * cols + x] = 7; }
        }
        // Replaces the draft with a room; an object outside the room is an error that leaves the draft for the caller to rebuild.
        void Fill(PhaseDemoRoom room)
        {
            Allocate(room.GridWidth / 8, room.GridHeight / 8);
            void Put(double x, double y, double w, double h, int tool, int mask)
            {
                for (int cy = (int)y / 8; cy < (y + h) / 8; cy++)
                    for (int cx = (int)x / 8; cx < (x + w) / 8; cx++)
                    {
                        if (cx < 0 || cy < 0 || cx >= cols || cy >= rows) throw new ArgumentException("物件超出地圖範圍。");
                        tools[cy * cols + cx] = (byte)tool; colors[cy * cols + cx] = (byte)mask;
                    }
            }
            foreach (var block in room.blocks) Put(block.x, block.y, block.w, block.h, block.spiked ? Covered : Terrain, block.Mask);
            foreach (var spike in room.CreateSpikes()) Put(spike.x, spike.y, spike.w, spike.h, 2 + (int)spike.direction, spike.Mask);
            foreach (var laser in room.lasers ?? Array.Empty<DemoLaser>()) Put(laser.x, laser.y, 8, 8, LaserTool + (int)laser.direction, laser.phaseMask);
            foreach (var crystal in room.crystals ?? Array.Empty<DemoCrystal>()) Put(crystal.x, crystal.y, 8, 8, CrystalTool, crystal.phaseMask);
            foreach (var platform in room.platforms ?? Array.Empty<DemoPlatform>()) PhaseCustomMap.CheckPlatform(platform, room.GridWidth, room.GridHeight);
            platforms.AddRange(room.platforms ?? Array.Empty<DemoPlatform>());
            SpawnCell = new Vector2Int(Math.Max(0, Math.Min(cols - 1, (int)room.spawnX / 8)), Math.Max(0, Math.Min(rows - 2, (int)room.spawnY / 8)));
        }
        void Allocate(int columns, int rowCount)
        {
            cols = columns; rows = rowCount; tools = new byte[cols * rows]; colors = new byte[cols * rows];
            Scroll = Vector2Int.zero; scene = null; platforms.Clear(); CancelPlacing();
        }
        void Save()
        {
            // A draft that cannot be written keeps the previous one rather than failing every frame.
            try { PlayerPrefs.SetString(DraftKey, PhaseCustomMap.ToJson(Room())); PlayerPrefs.Save(); }
            catch (ArgumentException) { }
            dirty = false;
        }
        // The map as authored; a nonzero down shifts everything below the room's top, which the scrolling preview uses.
        public PhaseDemoRoom Room(int down = 0)
        {
            var blocks = new List<Block>(); var spikes = new List<DemoSpike>(); var lasers = new List<DemoLaser>(); var crystals = new List<DemoCrystal>();
            for (int i = 0; i < tools.Length; i++)
            {
                int x = i % cols * 8, y = i / cols * 8 + down, tool = tools[i];
                if (tool == Terrain || tool == Covered) blocks.Add(new Block(x, y, 8, 8, -1, colors[i]) { spiked = tool == Covered });
                else if (tool == CrystalTool) crystals.Add(new DemoCrystal { x = x, y = y, phaseMask = colors[i] });
                else if (tool >= LaserTool) lasers.Add(new DemoLaser { x = x, y = y, direction = (LaserDirection)(tool - LaserTool), phaseMask = colors[i] });
                else if (tool != Erase) spikes.Add(new DemoSpike { x = x, y = y, length = 8, direction = (SpikeDirection)(tool - 2), phaseMask = colors[i] });
            }
            return new PhaseDemoRoom { name = "自訂地圖", width = cols * 8, height = rows * 8, blocks = blocks.ToArray(), spikes = spikes.ToArray(), lasers = lasers.ToArray(), crystals = crystals.ToArray(), platforms = platforms.ToArray(),
                spawnX = SpawnCell.x * 8, spawnY = SpawnCell.y * 8 + down };
        }
        // Content changed: the draft needs saving and the preview scene a rebuild.
        bool Edited() { dirty = true; scene = null; return true; }
        static int KindOf(int tool) => tool >= SpikeTool && tool < Spawn ? SpikeTool : tool >= LaserTool && tool < CrystalTool ? LaserTool : tool;
        static bool Turns(int kind) => kind == SpikeTool || kind == LaserTool;
        int ToolFor(int kind) => Turns(kind) ? kind + Facing : kind;
        void Select(int kind) { CancelPlacing(); Tool = ToolFor(kind); }
        // One clockwise (1) or counter-clockwise (-1) turn; other types ignore it.
        bool Rotate(int turn)
        {
            if (!Turns(KindOf(Tool))) return false;
            Facing = clockwise[(Array.IndexOf(clockwise, Facing) + turn + 4) % 4];
            Tool = ToolFor(KindOf(Tool)); return true;
        }
        void CancelPlacing() { draft.Clear(); draftLength = 0; }
        static bool Covers(Block b, Vector2Int cell) => cell.x * 8 >= b.x && cell.x * 8 < b.x + b.w && cell.y * 8 >= b.y && cell.y * 8 < b.y + b.h;
        // The platform whose body, or attached spike row (side), occupies the cell at its first pivot.
        // With anyStop every stop of the path counts, so each drawn stop can be edited; otherwise only the first, where the platform sits.
        (DemoPlatform platform, SpikeDirection? side) PlatformAt(Vector2Int cell, bool anyStop = false)
        {
            foreach (var platform in platforms)
            {
                var body = platform.Create(); var home = body.OriginAt(0);
                for (int i = 0; i < (anyStop ? body.pivots.Length : 1); i++)
                {
                    var at = body.OriginAt(i); var local = new Vector2Int(cell.x - (int)(at.x - home.x) / 8, cell.y - (int)(at.y - home.y) / 8);
                    if (Covers(body, local)) return (platform, null);
                    foreach (var teeth in body.attached) if (Covers(teeth, local)) return (platform, teeth.direction);
                }
            }
            return (null, null);
        }
        // A new spike row needs every cell of its row inside the room and empty of terrain, objects and other platforms.
        bool RowFree(DemoPlatform platform, SpikeDirection face)
        {
            var row = new Platform(platform.vertical, platform.length, platform.pivots, platform.reverse, platform.phaseMask, false, new[] { face }).attached[0];
            for (int y = (int)row.y / 8; y < (row.y + row.h) / 8; y++)
                for (int x = (int)row.x / 8; x < (row.x + row.w) / 8; x++)
                    if (x < 0 || y < 0 || x >= cols || y >= rows || tools[y * cols + x] != Erase || PlatformAt(new Vector2Int(x, y)).platform != null) return false;
            // The row travels with the platform, so it must stay inside the room at every stop.
            var body = platform.Create(); var home = body.OriginAt(0);
            for (int i = 1; i < body.pivots.Length; i++)
            {
                var at = body.OriginAt(i);
                double left = row.x + at.x - home.x, top = row.y + at.y - home.y;
                if (left < 0 || top < 0 || left + row.w > cols * 8 || top + row.h > rows * 8) return false;
            }
            return true;
        }
        // Every stop's body and spike rows inside the room: the same rule a map file is held to.
        static bool Fits(DemoPlatform platform, int columns, int rowCount)
        {
            try { PhaseCustomMap.CheckPlatform(platform, columns * 8, rowCount * 8); return true; }
            catch (ArgumentException) { return false; }
        }
        // Clicks place a platform: the first pivot, a body end on its row or column that sets the length, more pivots, then the first again.
        bool PlaceStep(Vector2Int cell)
        {
            if (draft.Count == 0) { draft.Add(cell); draftLength = 0; return true; }
            var first = draft[0];
            if (draftLength == 0)
            {
                if (cell.y == first.y) { draftVertical = false; draftLength = Math.Abs(cell.x - first.x) * 2 + 1; }
                else if (cell.x == first.x) { draftVertical = true; draftLength = Math.Abs(cell.y - first.y) * 2 + 1; }
                else flash = .4f;
                return true;
            }
            if (cell != first && draft.Contains(cell)) { flash = .4f; return true; }
            if (cell != first) { draft.Add(cell); return true; }
            if (draft.Count < 2) { flash = .4f; return true; }
            var made = new DemoPlatform { vertical = draftVertical, length = draftLength, phaseMask = Mask, pivots = draft.Select(c => (c.x, c.y)).ToArray() };
            var body = made.Create();
            bool free = Fits(made, cols, rows) && Enumerable.Range(0, (int)(body.w * body.h / 64)).All(n =>
            {
                var at = new Vector2Int((int)body.x / 8 + (draftVertical ? 0 : n), (int)body.y / 8 + (draftVertical ? n : 0));
                return tools[at.y * cols + at.x] == Erase && PlatformAt(at).platform == null;
            });
            if (!free) { flash = .4f; return true; }
            platforms.Add(made); CancelPlacing(); return Edited();
        }
        // The cover tool flips terrain or a platform between plain and spike-covered, or paints covered terrain on an empty cell.
        // A stroke keeps the direction its first cell chose.
        bool Cover(Vector2Int cell, bool press)
        {
            var (platform, side) = PlatformAt(cell, true);
            if (platform != null)
            {
                if (side != null) return false;
                if (press) coverOn = !platform.spiked;
                if (platform.spiked == coverOn) return false;
                platform.spiked = coverOn; return Edited();
            }
            int i = cell.y * cols + cell.x;
            if (tools[i] != Erase && tools[i] != Terrain && tools[i] != Covered) return false;
            if (press) coverOn = tools[i] != Covered;
            if (tools[i] == Erase && !coverOn) return false;
            byte tool = (byte)(coverOn ? Covered : Terrain), color = tools[i] == Erase ? (byte)Mask : colors[i];
            if (tools[i] == tool && colors[i] == color) return false;
            tools[i] = tool; colors[i] = color; return Edited();
        }
        bool Apply(Vector2Int cell, int tool, bool press = false)
        {
            if (tool == Spawn)
            {
                var spawn = new Vector2Int(cell.x, Math.Min(cell.y, rows - 2));
                if (spawn == SpawnCell) return false;
                SpawnCell = spawn; return Edited();
            }
            if (tool == PlatformTool) return press && PlaceStep(cell);
            if (tool == CoverTool) return Cover(cell, press);
            var (platform, side) = PlatformAt(cell, true);
            // Erasing takes the platform, or the spike row under the cursor; the spike tool flips the row on the face it points to.
            if (platform != null && tool == Erase)
            {
                if (side is SpikeDirection row) platform.spikes = platform.spikes.Where(d => d != row).ToArray(); else platforms.Remove(platform);
                return Edited();
            }
            if (platform != null && side == null && KindOf(tool) == SpikeTool)
            {
                if (!press) return false;
                var face = (SpikeDirection)(tool - SpikeTool);
                if (platform.spikes.Contains(face)) platform.spikes = platform.spikes.Where(d => d != face).ToArray();
                else if (RowFree(platform, face)) platform.spikes = platform.spikes.Append(face).ToArray();
                else { flash = .4f; return true; }
                return Edited();
            }
            // Where a platform starts, the cell is taken.
            if (PlatformAt(cell).platform != null) return false;
            int i = cell.y * cols + cell.x; byte color = (byte)(tool == Erase ? 0 : Mask);
            if (tools[i] == tool && colors[i] == color) return false;
            tools[i] = (byte)tool; colors[i] = color; return Edited();
        }

        // A fixed panel of 2× icons sits right of the grid, or below it on tall screens, and never covers a cell.
        const int Pitch = 18, Gap = 6, Pad = 4, SideW = Pad * 2 + 4 * Pitch, SideH = Pad * 2 + 6 * Pitch + 3 * Gap,
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
            var cell = Side(view) ? new Vector2Int(i % 4 * Pitch, (new[] { 0, 2, 4, 5 }[group] + i / 4) * Pitch + group * Gap)
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
            scene = new PhaseController(room.blocks, room.spawnX, room.spawnY, room.CreateSpikes(), room.CreateLasers(), room.CreateCrystals(), width, height, room.CreatePlatforms());
            // Room() shifts pixels, not pivot cells, so the platforms follow by hand.
            foreach (var platform in scene.platforms) platform.Move(0, Lift);
            return scene;
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
            if (Tool != Erase && Tool != Spawn) frame.Fill(cx, cy, 8, 8, color, .35f);
            if (Turns(KindOf(Tool))) frame.Blit(toolIcons[Tool], cx, cy, color, 1, .9f);
            frame.Outline(cx - 1, cy - 1, 10, 10, PixelFrame.Light, .9f);
            DrawPaths(frame, ox, oy);
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
                if (slot < MaskSlot) frame.Blit(toolIcons[ToolFor(kinds[slot])], x, y, kinds[slot] == Spawn || kinds[slot] == Erase ? PixelFrame.Light : color, 2, selected ? 1 : .45f);
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
        // Every platform's stops are outlined and joined by dotted arrows in the order it visits them; the one being placed is drawn the same, still open.
        void DrawPaths(PixelFrame frame, int ox, int oy)
        {
            foreach (var platform in platforms)
                DrawPath(frame, ox, oy, platform.pivots.Select(p => new Vector2Int(p.x, p.y)).ToList(), platform.vertical, platform.length, platform.reverse, true, PhasePixels.SurfaceColor(platform.phaseMask));
            if (Placing) DrawPath(frame, ox, oy, draft, draftVertical, Math.Max(1, draftLength), false, false, PhasePixels.SurfaceColor(Mask));
        }
        static void DrawPath(PixelFrame frame, int ox, int oy, List<Vector2Int> stops, bool vertical, int length, bool reverse, bool closed, Color32 color)
        {
            int half = length / 2, n = stops.Count;
            foreach (var stop in stops)
                frame.Outline((stop.x - (vertical ? 0 : half)) * 8 - ox, (stop.y - (vertical ? half : 0)) * 8 - oy, vertical ? 8 : length * 8, vertical ? length * 8 : 8, color, .5f);
            for (int k = 0; k < (closed ? n : n - 1); k++)
            {
                var from = stops[reverse ? (n - k) % n : k]; var to = stops[reverse ? (n - k - 1 + n) % n : (k + 1) % n];
                int ax = from.x * 8 + 4 - ox, ay = from.y * 8 + 4 - oy, bx = to.x * 8 + 4 - ox, by = to.y * 8 + 4 - oy;
                int steps = Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay)), dx = Math.Sign(bx - ax), dy = Math.Sign(by - ay);
                if (steps == 0) continue;
                for (int t = 0; t <= steps; t += 2) frame.Fill(ax + (bx - ax) * t / steps, ay + (by - ay) * t / steps, 1, 1, color, .8f);
                if (steps < 6) continue;
                int tipX = (ax + bx) / 2 + dx * 2, tipY = (ay + by) / 2 + dy * 2;
                for (int wing = 0; wing < 3; wing++)
                {
                    frame.Fill(tipX - dx * wing - dy * wing, tipY - dy * wing + dx * wing, 1, 1, color);
                    frame.Fill(tipX - dx * wing + dy * wing, tipY - dy * wing - dx * wing, 1, 1, color);
                }
            }
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
        // A smaller room must still hold every cell and the spawn, so shrinking never deletes anything.
        bool Holds(int columns, int rowCount)
        {
            for (int i = 0; i < tools.Length; i++) if (tools[i] != Erase && (i % cols >= columns || i / cols >= rowCount)) return false;
            return SpawnCell.x < columns && SpawnCell.y <= rowCount - 2 && platforms.All(p => Fits(p, columns, rowCount));
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
            Edited();
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
        void Leave() { CancelPlacing(); if (dirty) Save(); Open(PhaseTitle.Scene); }
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
                if (k.escapeKey.wasPressedThisFrame) { if (Placing) { CancelPlacing(); Refresh(); } else Leave(); return; }
                if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) { StartPlaytest(); return; }
                for (int i = 0; i < masks.Length; i++) if (k[Key.Digit1 + i].wasPressedThisFrame) { Mask = masks[i]; changed = true; }
                // R/E turn the tool and X cycles the type, even when bound to an action; that action waits until they are released.
                if (k.rKey.wasPressedThisFrame) changed |= Rotate(1);
                if (k.eKey.wasPressedThisFrame) changed |= Rotate(-1);
                bool turning = k.rKey.isPressed || k.eKey.isPressed || k.xKey.isPressed || k.fKey.isPressed;
                if (k.fKey.wasPressedThisFrame && PlatformAt(Cursor, true).platform is DemoPlatform loop) { loop.reverse = !loop.reverse; changed |= Edited(); }
                if (k.xKey.wasPressedThisFrame) { Select(kinds[(Array.IndexOf(kinds, KindOf(Tool)) + 1) % kinds.Length]); changed = true; }
                var input = turning ? default : PhaseSandbox.ReadControls(k);
                // Up and down are not bindable, so the cursor takes them from the arrow keys.
                var step = new Vector2Int(input.x, turning ? 0 : (k.downArrowKey.isPressed ? 1 : 0) - (k.upArrowKey.isPressed ? 1 : 0));
                if (step != Vector2Int.zero && (step != held || Time.unscaledTime >= repeatAt))
                {
                    repeatAt = Time.unscaledTime + (step != held ? .25f : .06f);
                    Cursor = new Vector2Int(Math.Max(0, Math.Min(cols - 1, Cursor.x + step.x)), Math.Max(0, Math.Min(rows - 1, Cursor.y + step.y)));
                    Reveal(Cursor); changed = true;
                }
                held = step;
                // Jump paints and C erases.
                bool erasing = !turning && k.cKey.isPressed;
                painting = input.jumpHeld || erasing;
                if (input.jumpHeld) changed |= Apply(Cursor, Tool, PhaseSandbox.Pressed(k, 2, true));
                else if (erasing) changed |= Apply(Cursor, Erase);
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
                if (m.rightButton.wasPressedThisFrame && Placing) { CancelPlacing(); stroke = false; changed = true; }
                if (m.leftButton.wasPressedThisFrame && slot is int chosen) { Choose(chosen); return; }
                if (onGrid && p != lastMouse) { Cursor = new Vector2Int(x, y); changed = true; }
                lastMouse = p;
                if (onGrid && stroke && m.leftButton.isPressed) { painting = true; changed |= Apply(Cursor, Tool, m.leftButton.wasPressedThisFrame); }
                else if (onGrid && stroke && m.rightButton.isPressed) { painting = true; changed |= Apply(Cursor, Erase); }
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
            try { room = PhaseCustomMap.ParseRoom(json); }
            catch (ArgumentException) { flash = .4f; Refresh(); return; }
            if (room.goal != null || room.extraSpawns != null) { flash = .4f; Refresh(); return; }
            Fill(room);
            Cursor = new Vector2Int(Math.Min(Cursor.x, cols - 1), Math.Min(Cursor.y, rows - 1));
            Save(); Refresh();
        }
        public bool StartPlaytest()
        {
            var room = Room();
            try { PhaseCustomMap.Validate(room); }
            catch (ArgumentException error) { Refuse(error); return false; }
            Play(PhaseCustomMap.ToWorld(room)); return true;
        }
        void Play(PhaseWorld world)
        {
            CancelPlacing();
            if (dirty) Save();
            surface.enabled = false;
            try { Playtest = PhaseSandbox.CreateCustom(world, pixelCamera, pixelMaterial, transform); }
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
                var map = PhaseCustomMap.Parse(json);
                StopPlaytest(); Play(map);
                PhaseSandbox.Pause("false");
                PhaseSandbox.MapResult(true);
            }
            catch (Exception error) { PhaseSandbox.MapResult(false, error.Message); }
        }
    }
}
