using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Phase
{
    // Paints terrain, spikes, lasers, jump crystals, jump platforms, spawns and the goal on a world of rooms, then playtests it in place.
    // The canvas is the whole world in cells; rooms sit on a grid of 40×22-cell screens.
    public sealed class PhaseEditor : PhaseScreen
    {
        public const string Scene = "PhaseEditor", DraftKey = "phase.editor.v1";
        // One screen of cells is the window the keyboard cursor keeps in view; the frame shows more of the world around it.
        public const int Columns = 40, Rows = 22;
        // Cell tools: 0 empty, 1 terrain, 2–5 Up/Down/Left/Right spikes, 7–10 Up/Down/Left/Right lasers, 11 jump crystal, 12 spike-covered terrain.
        // Spawn, GoalTool, PlatformTool and CoverTool store nothing in a cell (spawns and the goal are kept per room, platforms apart, CoverTool
        // paints or toggles cell 12 and platform covers); RoomTool merges and splits rooms at a seam and MoveTool moves whole rooms.
        // The panel holds one slot per type; R/E or the wheel turns spikes and lasers.
        public const int Erase = 0, Terrain = 1, SpikeTool = 2, Spawn = 6, LaserTool = 7, CrystalTool = 11, Covered = 12, PlatformTool = 13, CoverTool = 14,
            GoalTool = 15, RoomTool = 16, MoveTool = 17, LastTool = 11;
        static readonly int[] kinds = { Terrain, SpikeTool, LaserTool, CrystalTool, PlatformTool, CoverTool, Spawn, Erase, GoalTool, RoomTool, MoveTool },
            masks = { 7, 1, 2, 4, 5, 3, 6 }, clockwise = { 0, 3, 1, 2 };
        // Panel slots: 11 types, 7 colors, then import, export, play and back.
        public const int MaskSlot = 11, ImportSlot = 18, ExportSlot = 19, PlaySlot = 20, BackSlot = 21, SlotCount = 22;
        static readonly string[] spikeUp = { ".......", "...#...", "..###..", "..###..", ".#####.", ".#####.", "#######" },
            laserUp = { "...#...", "...#...", "...#...", ".#####.", ".#...#.", ".#.#.#.", ".#####." },
            spawnIcon = { "#......", "####...", "#####..", "####...", "#......", "#......", "#......" },
            platformIcon = { ".......", "#######", "#######", ".......", "..###..", "...#...", "......." },
            coverIcon = { "..#.#..", ".#####.", "#######", ".#####.", "#######", ".#####.", "..#.#.." },
            goalIcon = { "#.#.#.#", ".#.#.#.", "#.#.#.#", "#......", "#......", "#......", "#......" },
            roomIcon = { "###.###", "#.#.#.#", "#.#.#.#", "#.#.#.#", "#.#.#.#", "#.#.#.#", "###.###" },
            moveIcon = { "...#...", "..###..", ".#.#.#.", "#######", ".#.#.#.", "..###..", "...#..." };
        // Indexed by tool code: erase, terrain, four spikes (Up, Down, Left, Right), spawn, four lasers, crystal, covered terrain, platform, cover, goal, room, move.
        static readonly string[][] toolIcons = {
            new[] { "#.....#", ".#...#.", "..#.#..", "...#...", "..#.#..", ".#...#.", "#.....#" },
            Enumerable.Repeat("#######", 7).ToArray(), spikeUp, spikeUp.Reverse().ToArray(), Turn(spikeUp), Mirror(Turn(spikeUp)), spawnIcon,
            laserUp, laserUp.Reverse().ToArray(), Turn(laserUp), Mirror(Turn(laserUp)),
            new[] { "..###..", ".#####.", "##.####", "#######", "#######", ".#####.", "..###.." },
            coverIcon, platformIcon, coverIcon, goalIcon, roomIcon, moveIcon
        };
        static readonly string[] backIcon = { "...#...", "..#....", ".#.....", "#######", ".#.....", "..#....", "...#..." },
            exportIcon = { "....###....", "....###....", "....###....", ".#########.", "..#######..", "...#####...", "....###....", ".....#.....", "##.......##", "##.......##", "###########" },
            importIcon = { ".....#.....", "....###....", "...#####...", "..#######..", ".#########.", "....###....", "....###....", "....###....", "##.......##", "##.......##", "###########" };

        // A room of the world: its screen position and size, then room-local cells, platforms, spawns and the goal.
        sealed class EditRoom
        {
            public int atX, atY, w = 1, h = 1;
            public byte[] tools = new byte[Columns * Rows], colors = new byte[Columns * Rows];
            public readonly List<DemoPlatform> platforms = new List<DemoPlatform>();
            public readonly List<Vector2Int> spawns = new List<Vector2Int>();
            public Vector2Int? goal;
            public int Cols => w * Columns;
            public int RowCount => h * Rows;
            public int OriginX => atX * Columns;
            public int OriginY => atY * Rows;
            public bool Empty => platforms.Count == 0 && spawns.Count == 0 && goal == null && tools.All(t => t == Erase);
            public EditRoom(int x, int y, int w = 1, int h = 1)
            { atX = x; atY = y; this.w = w; this.h = h; tools = new byte[Cols * RowCount]; colors = new byte[Cols * RowCount]; }
        }
        readonly List<EditRoom> rooms = new List<EditRoom>();
        // The room the cell tools are working in; its cells, size and platforms are what the cell helpers below see.
        EditRoom cur;
        byte[] tools => cur.tools;
        byte[] colors => cur.colors;
        int cols => cur.Cols;
        int rows => cur.RowCount;
        List<DemoPlatform> platforms => cur.platforms;
        // The platform being placed: the centre cells clicked so far (room-local, in draftRoom), and once the second click set it, its orientation and length.
        readonly List<Vector2Int> draft = new List<Vector2Int>();
        EditRoom draftRoom;
        bool draftVertical, coverOn;
        int draftLength;
        public bool Placing => draft.Count > 0;
        // 0 until the second click gives the platform being placed its length.
        public int DraftLength => draftLength;
        public int Tool { get; private set; } = Terrain;
        // The spike/laser direction index (Up, Down, Left, Right), kept across type changes.
        public int Facing { get; private set; }
        public int Mask { get; private set; } = 7;
        public int RoomCount => rooms.Count;
        // The world cell shown at the frame's window top-left corner, and the cell under the cursor.
        public Vector2Int Scroll { get; private set; }
        public Vector2Int Cursor { get; private set; } = new Vector2Int(8, 18);
        public PhaseSandbox Playtest { get; private set; }
        // The panel slot under the mouse.
        public int? Hover { get; private set; }
        public bool Overview { get; private set; }
        public bool Flashing => flash > 0;
        // Indices of the rooms chosen with the move tool.
        public IReadOnlyList<int> Selection => selection.Select(r => rooms.IndexOf(r)).OrderBy(i => i).ToList();
        protected override bool Hidden => Playtest != null;
        readonly List<EditRoom> selection = new List<EditRoom>();
        // The rooms that refused a playtest or export for lacking a spawn; their frames blink with the border.
        readonly List<EditRoom> missing = new List<EditRoom>();
        Vector2Int held, lastMouse = new Vector2Int(-1, -1), panScroll;
        // Where a pan began, while its button is held; and where a room drag began, in world cells.
        Vector2Int? panFrom, dragFrom;
        float repeatAt, flash;
        // A mouse stroke paints or erases only when its press began on the grid.
        bool dirty, stroke;
        // A refused map whose spawn overlaps something: the spawn markers and the flag slot blink with the border.
        public bool SpawnBlocked { get; private set; }

        static string[] Turn(string[] art) => Enumerable.Range(0, 7).Select(r => new string(Enumerable.Range(0, 7).Select(c => art[c][r]).ToArray())).ToArray();
        static string[] Mirror(string[] art) => art.Select(r => new string(r.Reverse().ToArray())).ToArray();
        static int FloorDiv(int a, int b) => (int)Math.Floor(a / (double)b);
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
                try { Fill(PhaseCustomMap.Parse(saved, false)); return; }
                catch (ArgumentException) { }
            }
            var parts = saved.Split(new[] { ';' }, 2);
            var spawn = parts[0].Split(',');
            // A larger room appends its columns and rows to the spawn cell; older drafts hold a 40×22 grid.
            int c = Columns, r = Rows;
            bool sized = spawn.Length == 4 && int.TryParse(spawn[2], out c) && int.TryParse(spawn[3], out r);
            if (parts.Length == 2 && (spawn.Length == 2 || sized) && c >= Columns && c <= PhaseCustomMap.MaxWidth / 8 && r >= Rows && r <= PhaseCustomMap.MaxHeight / 8 && parts[1].Length >= c * r * 2 &&
                int.TryParse(spawn[0], out int sx) && int.TryParse(spawn[1], out int sy) && sx >= 0 && sx < c && sy >= 0 && sy <= r - 2)
            {
                // The grid becomes one room: whole screens, the content at the bottom and the extra columns on the right.
                var room = new EditRoom(0, 0, (c + Columns - 1) / Columns, (r + Rows - 1) / Rows);
                int pad = room.RowCount - r; bool valid = true;
                for (int i = 0; i < c * r && valid; i++)
                {
                    int tool = parts[1][i * 2] - '0', mask = parts[1][i * 2 + 1] - '0';
                    // Older drafts may hold a dash crystal (12), which is gone; its cell is left empty.
                    if (tool == 12) tool = mask = Erase;
                    valid = tool == Erase ? mask == 0 : tool > 0 && tool <= LastTool && tool != Spawn && mask >= 1 && mask <= 7;
                    int at = (i / c + pad) * room.Cols + i % c;
                    room.tools[at] = (byte)tool; room.colors[at] = (byte)mask;
                }
                // Older drafts may append dash platforms and gears after the cells; they are dropped.
                if (valid) { room.spawns.Add(new Vector2Int(sx, sy + pad)); rooms.Add(room); return; }
            }
            var start = new EditRoom(0, 0); rooms.Add(start);
            for (int x = 0; x < start.Cols; x++) { start.tools[(start.RowCount - 1) * start.Cols + x] = Terrain; start.colors[(start.RowCount - 1) * start.Cols + x] = 7; }
            start.spawns.Add(new Vector2Int(3, 19));
        }
        // Replaces the draft with a world; an object outside its room is an error that leaves the draft for the caller to rebuild.
        void Fill(PhaseWorld world)
        {
            var made = new List<EditRoom>();
            foreach (var room in world.rooms)
            {
                var edit = new EditRoom(room.atX, room.atY, room.GridWidth / PhaseWorld.ScreenWidth, room.GridHeight / PhaseWorld.ScreenHeight);
                void Put(double x, double y, double w, double h, int tool, int mask)
                {
                    for (int cy = (int)y / 8; cy < (y + h) / 8; cy++)
                        for (int cx = (int)x / 8; cx < (x + w) / 8; cx++)
                        {
                            if (cx < 0 || cy < 0 || cx >= edit.Cols || cy >= edit.RowCount) throw new ArgumentException("物件超出地圖範圍。");
                            edit.tools[cy * edit.Cols + cx] = (byte)tool; edit.colors[cy * edit.Cols + cx] = (byte)mask;
                        }
                }
                foreach (var block in room.blocks) Put(block.x, block.y, block.w, block.h, block.spiked ? Covered : Terrain, block.Mask);
                foreach (var spike in room.CreateSpikes()) Put(spike.x, spike.y, spike.w, spike.h, 2 + (int)spike.direction, spike.Mask);
                foreach (var laser in room.lasers ?? Array.Empty<DemoLaser>()) Put(laser.x, laser.y, 8, 8, LaserTool + (int)laser.direction, laser.phaseMask);
                foreach (var crystal in room.crystals ?? Array.Empty<DemoCrystal>()) Put(crystal.x, crystal.y, 8, 8, CrystalTool, crystal.phaseMask);
                foreach (var platform in room.platforms ?? Array.Empty<DemoPlatform>()) PhaseCustomMap.CheckPlatform(platform, room.GridWidth, room.GridHeight);
                edit.platforms.AddRange(room.platforms ?? Array.Empty<DemoPlatform>());
                foreach (var (x, y) in room.Spawns)
                    edit.spawns.Add(new Vector2Int(Math.Max(0, Math.Min(edit.Cols - 1, (int)x / 8)), Math.Max(0, Math.Min(edit.RowCount - 2, (int)y / 8))));
                if (room.goal != null) edit.goal = new Vector2Int((int)room.goal.x / 8, (int)room.goal.y / 8);
                made.Add(edit);
            }
            rooms.Clear(); rooms.AddRange(made); selection.Clear(); missing.Clear();
            Scroll = Vector2Int.zero; scene = null; CancelPlacing();
            var first = rooms[world.Start]; Scroll = new Vector2Int(first.OriginX, first.OriginY); Cursor = Scroll + new Vector2Int(8, 18);
        }
        void Save()
        {
            // A draft that cannot be written keeps the previous one rather than failing every frame.
            try
            {
                if (rooms.Count == 0) PlayerPrefs.DeleteKey(DraftKey); else PlayerPrefs.SetString(DraftKey, PhaseCustomMap.ToJson(World()));
                PlayerPrefs.Save();
            }
            catch (ArgumentException) { }
            dirty = false;
        }
        // The map as authored. A room without a spawn stays without one; playtest and export refuse such a room.
        public PhaseWorld World()
        {
            var made = new List<PhaseDemoRoom>();
            foreach (var room in rooms)
            {
                var blocks = new List<Block>(); var spikes = new List<DemoSpike>(); var lasers = new List<DemoLaser>(); var crystals = new List<DemoCrystal>();
                for (int i = 0; i < room.tools.Length; i++)
                {
                    int x = i % room.Cols * 8, y = i / room.Cols * 8, tool = room.tools[i];
                    if (tool == Terrain || tool == Covered) blocks.Add(new Block(x, y, 8, 8, -1, room.colors[i]) { spiked = tool == Covered });
                    else if (tool == CrystalTool) crystals.Add(new DemoCrystal { x = x, y = y, phaseMask = room.colors[i] });
                    else if (tool >= LaserTool) lasers.Add(new DemoLaser { x = x, y = y, direction = (LaserDirection)(tool - LaserTool), phaseMask = room.colors[i] });
                    else if (tool != Erase) spikes.Add(new DemoSpike { x = x, y = y, length = 8, direction = (SpikeDirection)(tool - 2), phaseMask = room.colors[i] });
                }
                var spawns = room.spawns.OrderBy(s => s.x).ThenBy(s => s.y).ToList();
                made.Add(new PhaseDemoRoom {
                    atX = room.atX, atY = room.atY, width = room.w * PhaseWorld.ScreenWidth, height = room.h * PhaseWorld.ScreenHeight,
                    blocks = blocks.ToArray(), spikes = spikes.ToArray(), lasers = lasers.ToArray(), crystals = crystals.ToArray(), platforms = room.platforms.ToArray(),
                    noSpawn = spawns.Count == 0, spawnX = spawns.Count > 0 ? spawns[0].x * 8 : 0, spawnY = spawns.Count > 0 ? spawns[0].y * 8 : 0,
                    extraSpawns = spawns.Count > 1 ? spawns.Skip(1).Select(s => new DemoPoint { x = s.x * 8, y = s.y * 8 }).ToArray() : null,
                    goal = room.goal is Vector2Int g ? new DemoPoint { x = g.x * 8, y = g.y * 8 } : null
                });
            }
            return new PhaseWorld { name = "自訂地圖", rooms = made.ToArray() };
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
        void CancelPlacing() { draft.Clear(); draftLength = 0; var room = draftRoom; draftRoom = null; if (room != null) Prune(room); }
        // A room whose content was all erased, or that a tool created without changing anything, is gone.
        void Prune(EditRoom room)
        {
            if (!room.Empty || (Placing && room == draftRoom)) return;
            rooms.Remove(room); selection.Remove(room); missing.Remove(room); scene = null; dirty = true;
        }
        EditRoom RoomAtCell(Vector2Int cell) => rooms.FirstOrDefault(r => cell.x >= r.OriginX && cell.x < r.OriginX + r.Cols && cell.y >= r.OriginY && cell.y < r.OriginY + r.RowCount);
        EditRoom RoomAtScreen(int sx, int sy) => rooms.FirstOrDefault(r => sx >= r.atX && sx < r.atX + r.w && sy >= r.atY && sy < r.atY + r.h);
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
            if (draft.Count == 0) { draft.Add(cell); draftLength = 0; draftRoom = cur; return true; }
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
        bool Apply(Vector2Int world, int tool, bool press = false)
        {
            if (tool == RoomTool) return press && AtSeam(world);
            if (tool == MoveTool) return press && Pick(world, Shift);
            var room = RoomAtCell(world); bool made = false;
            if (room == null)
            {
                if (tool == Erase) return false;
                if (rooms.Count >= PhaseWorld.MaxRooms || (Placing && tool == PlatformTool)) { flash = .4f; return true; }
                room = new EditRoom(FloorDiv(world.x, Columns), FloorDiv(world.y, Rows)); rooms.Add(room); made = true;
            }
            if (Placing && tool == PlatformTool && room != draftRoom) { flash = .4f; return true; }
            cur = room;
            bool changed = ApplyIn(room, new Vector2Int(world.x - room.OriginX, world.y - room.OriginY), tool, press);
            if (made && changed) { dirty = true; scene = null; }
            if (tool == Erase || (made && !changed) || (made && room.Empty && !Placing)) Prune(room);
            return changed || made;
        }
        bool ApplyIn(EditRoom room, Vector2Int cell, int tool, bool press)
        {
            if (tool == Spawn)
            {
                if (!press) return false;
                var spawn = new Vector2Int(cell.x, Math.Min(cell.y, rows - 2));
                if (room.spawns.Contains(spawn)) { room.spawns.Remove(spawn); return Edited(); }
                room.spawns.Add(spawn); return Edited();
            }
            if (tool == GoalTool)
            {
                if (!press) return false;
                if (room.goal == cell) { room.goal = null; return Edited(); }
                if (rooms.Any(r => r.goal != null)) { flash = .4f; return true; }
                room.goal = cell; return Edited();
            }
            if (tool == PlatformTool) return press && PlaceStep(cell);
            if (tool == CoverTool) return Cover(cell, press);
            // Erasing also takes a spawn or the goal on the cell.
            bool marker = tool == Erase && (room.spawns.Remove(cell) | (room.goal == cell && (room.goal = null) == null));
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
            if (PlatformAt(cell).platform != null) return marker && Edited();
            int i = cell.y * cols + cell.x; byte color = (byte)(tool == Erase ? 0 : Mask);
            if (tools[i] == tool && colors[i] == color) return marker && Edited();
            tools[i] = (byte)tool; colors[i] = color; return Edited();
        }
        bool Shift => Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

        // The room tool: a click within three cells of a seam between two screens merges the rooms on either side, or splits a room that holds both.
        bool AtSeam(Vector2Int world)
        {
            int sx = FloorDiv(world.x, Columns), sy = FloorDiv(world.y, Rows), lx = world.x - sx * Columns, ly = world.y - sy * Rows;
            var near = new[] { (d: lx, x: -1, y: 0), (d: Columns - 1 - lx, x: 1, y: 0), (d: ly, x: 0, y: -1), (d: Rows - 1 - ly, x: 0, y: 1) }.OrderBy(e => e.d).First();
            if (near.d > 2) return false;
            var a = RoomAtScreen(sx, sy); var b = RoomAtScreen(sx + near.x, sy + near.y);
            if (a == null || b == null) return false;
            if (a == b) return Split(a, near.x != 0, near.x != 0 ? Math.Max(sx, sx + near.x) : Math.Max(sy, sy + near.y));
            return Merge(a, b);
        }
        // Rooms a and b become one room covering both; it may not cover another room or outgrow 4×5 screens.
        bool Merge(EditRoom a, EditRoom b)
        {
            int x0 = Math.Min(a.atX, b.atX), y0 = Math.Min(a.atY, b.atY), x1 = Math.Max(a.atX + a.w, b.atX + b.w), y1 = Math.Max(a.atY + a.h, b.atY + b.h);
            if (x1 - x0 > PhaseWorld.MaxScreensWide || y1 - y0 > PhaseWorld.MaxScreensTall || rooms.Any(r => r != a && r != b && r.atX < x1 && r.atX + r.w > x0 && r.atY < y1 && r.atY + r.h > y0))
            { flash = .4f; return true; }
            var merged = new EditRoom(x0, y0, x1 - x0, y1 - y0);
            foreach (var part in new[] { a, b })
            {
                int dx = part.OriginX - merged.OriginX, dy = part.OriginY - merged.OriginY;
                for (int i = 0; i < part.tools.Length; i++)
                {
                    if (part.tools[i] == Erase) continue;
                    int at = (i / part.Cols + dy) * merged.Cols + i % part.Cols + dx;
                    merged.tools[at] = part.tools[i]; merged.colors[at] = part.colors[i];
                }
                merged.spawns.AddRange(part.spawns.Select(s => s + new Vector2Int(dx, dy)));
                if (part.goal is Vector2Int g) merged.goal = g + new Vector2Int(dx, dy);
                foreach (var platform in part.platforms)
                    merged.platforms.Add(new DemoPlatform { vertical = platform.vertical, reverse = platform.reverse, spiked = platform.spiked, length = platform.length, phaseMask = platform.phaseMask,
                        spikes = platform.spikes, pivots = platform.pivots.Select(p => (p.x + dx, p.y + dy)).ToArray() });
            }
            int at0 = Math.Min(rooms.IndexOf(a), rooms.IndexOf(b));
            rooms.Remove(a); rooms.Remove(b); rooms.Insert(Math.Min(at0, rooms.Count), merged);
            bool chosen = selection.Remove(a) | selection.Remove(b);
            if (chosen) selection.Add(merged);
            missing.Remove(a); missing.Remove(b);
            return Edited();
        }
        // The room divides along the whole line of screens before `at` (a column for a vertical line, a row otherwise); a platform path may not cross it.
        bool Split(EditRoom room, bool vertical, int at)
        {
            var first = vertical ? new EditRoom(room.atX, room.atY, at - room.atX, room.h) : new EditRoom(room.atX, room.atY, room.w, at - room.atY);
            var second = vertical ? new EditRoom(at, room.atY, room.atX + room.w - at, room.h) : new EditRoom(room.atX, at, room.w, room.atY + room.h - at);
            int cut = vertical ? first.Cols : first.RowCount;
            var shift = vertical ? new Vector2Int(cut, 0) : new Vector2Int(0, cut);
            bool Second(Vector2Int c) => (vertical ? c.x : c.y) >= cut;
            for (int i = 0; i < room.tools.Length; i++)
            {
                if (room.tools[i] == Erase) continue;
                var c = new Vector2Int(i % room.Cols, i / room.Cols); var part = Second(c) ? second : first; var local = Second(c) ? c - shift : c;
                part.tools[local.y * part.Cols + local.x] = room.tools[i]; part.colors[local.y * part.Cols + local.x] = room.colors[i];
            }
            foreach (var s in room.spawns) (Second(s) ? second : first).spawns.Add(Second(s) ? s - shift : s);
            if (room.goal is Vector2Int g) (Second(g) ? second : first).goal = Second(g) ? g - shift : g;
            foreach (var platform in room.platforms)
            {
                var home = new Vector2Int(platform.pivots[0].x, platform.pivots[0].y); var part = Second(home) ? second : first; var by = Second(home) ? shift : Vector2Int.zero;
                var moved = new DemoPlatform { vertical = platform.vertical, reverse = platform.reverse, spiked = platform.spiked, length = platform.length, phaseMask = platform.phaseMask,
                    spikes = platform.spikes, pivots = platform.pivots.Select(p => (p.x - by.x, p.y - by.y)).ToArray() };
                if (!Fits(moved, part.Cols, part.RowCount)) { flash = .4f; return true; }
                part.platforms.Add(moved);
            }
            int index = rooms.IndexOf(room);
            rooms.Remove(room); rooms.Insert(index, first); rooms.Insert(index + 1, second);
            if (selection.Remove(room)) { selection.Add(first); selection.Add(second); }
            missing.Remove(room);
            return Edited();
        }
        // The move tool picks the room under the cell: alone, or added to or removed from the chosen rooms with Shift.
        bool Pick(Vector2Int world, bool add)
        {
            var room = RoomAtCell(world);
            if (room == null) { if (add || selection.Count == 0) return false; selection.Clear(); return true; }
            if (add) { if (!selection.Remove(room)) selection.Add(room); }
            else if (!selection.Contains(room)) { selection.Clear(); selection.Add(room); }
            return true;
        }
        // The chosen rooms move together by whole screens; a drop onto another room is refused.
        public bool MoveSelection(int dx, int dy)
        {
            if (selection.Count == 0 || (dx == 0 && dy == 0)) return false;
            if (rooms.Any(o => !selection.Contains(o) && selection.Any(r => r.atX + dx < o.atX + o.w && r.atX + dx + r.w > o.atX && r.atY + dy < o.atY + o.h && r.atY + dy + r.h > o.atY)))
            { flash = .4f; return true; }
            foreach (var room in selection) { room.atX += dx; room.atY += dy; }
            return Edited();
        }

        // A fixed panel of 2× icons sits right of the grid, or below it on tall screens, and never covers a cell.
        const int Pitch = 18, Gap = 6, Pad = 4, SideW = Pad * 2 + 4 * Pitch, SideH = Pad * 2 + 6 * Pitch + 2 * Gap,
            BelowTop = Rows * 8 + 4, BelowW = Pad * 2 + 11 * Pitch + Gap, BelowH = Pad * 2 + 2 * Pitch + Gap;
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
        // The window's top row in the frame.
        static int GridTop(Vector2Int view) => -((180 - view.y) / 2);
        public static RectInt PanelIn(Vector2Int view) => Side(view)
            ? new RectInt(Columns * 8, GridTop(view) + (Rows * 8 - SideH) / 2, SideW, SideH)
            : new RectInt((Columns * 8 - BelowW) / 2, GridTop(view) + BelowTop, BelowW, BelowH);
        // The side panel stacks tools, colors and files/play/back in four columns; below the grid the tools fill one row
        // and the colors share the next with files/play/back.
        public static RectInt SlotIn(Vector2Int view, int slot)
        {
            var panel = PanelIn(view);
            int group = slot < MaskSlot ? 0 : slot < ImportSlot ? 1 : 2, i = slot - new[] { 0, MaskSlot, ImportSlot }[group];
            var cell = Side(view) ? new Vector2Int(i % 4 * Pitch, (new[] { 0, 3, 5 }[group] + i / 4) * Pitch + group * Gap)
                : new Vector2Int((new[] { 0, 0, 7 }[group] + i) * Pitch + (group == 2 ? Gap : 0), group == 0 ? 0 : Pitch + Gap);
            return new RectInt(panel.x + Pad + cell.x, panel.y + Pad + cell.y, Pitch, Pitch);
        }
        protected override Vector2Int ViewSize(int width, int height) => ViewFor(width, height);
        Vector2Int View => new Vector2Int(pixels.Width, pixels.Height);
        // World pixel under the frame's top-left corner: the scroll plus the band PhasePixels centers vertically.
        Vector2Int Origin => new Vector2Int(Scroll.x * 8, Scroll.y * 8 - GridTop(View));
        PhaseController scene;
        Vector2Int sceneView;
        // The preview is a controller over exactly what the frame shows: every room's cells in view, moved so the frame's corner is (0, 0).
        PhaseController Preview()
        {
            if (scene != null && sceneView == View) return scene;
            sceneView = View; var origin = Origin;
            int width = Math.Max(320, (View.x + 7) / 8 * 8), height = View.y <= 180 ? 180 : (View.y + 7) / 8 * 8;
            int left = FloorDiv(origin.x, 8), right = FloorDiv(origin.x + View.x, 8), top = FloorDiv(origin.y, 8), bottom = FloorDiv(origin.y + View.y, 8);
            var blocks = new List<Block>(); var spikes = new List<Spike>(); var lasers = new List<Laser>(); var crystals = new List<Crystal>(); var moving = new List<Platform>();
            foreach (var room in rooms)
            {
                for (int cy = Math.Max(top, room.OriginY); cy <= Math.Min(bottom, room.OriginY + room.RowCount - 1); cy++)
                    for (int cx = Math.Max(left, room.OriginX); cx <= Math.Min(right, room.OriginX + room.Cols - 1); cx++)
                    {
                        int i = (cy - room.OriginY) * room.Cols + cx - room.OriginX, tool = room.tools[i], mask = room.colors[i];
                        int x = cx * 8 - origin.x, y = cy * 8 - origin.y;
                        if (tool == Terrain || tool == Covered) blocks.Add(new Block(x, y, 8, 8, -1, mask) { spiked = tool == Covered });
                        else if (tool == CrystalTool) crystals.Add(new Crystal(x, y, mask));
                        else if (tool >= LaserTool) lasers.Add(new Laser(x, y, (LaserDirection)(tool - LaserTool), mask));
                        else if (tool != Erase) spikes.Add(new Spike(x, y, 8, (SpikeDirection)(tool - 2), mask));
                    }
                foreach (var platform in room.platforms)
                {
                    var body = new Platform(platform.vertical, platform.length, platform.pivots.Select(p => (p.x + room.OriginX, p.y + room.OriginY)), platform.reverse, platform.phaseMask, platform.spiked, platform.spikes);
                    body.Move(-origin.x, -origin.y); moving.Add(body);
                }
            }
            scene = new PhaseController(blocks, -100, -100, spikes, lasers, crystals, width, height, moving) { flash = 0 };
            var goal = rooms.FirstOrDefault(r => r.goal != null);
            if (goal != null) scene.goal = new Block((goal.OriginX + goal.goal.Value.x) * 8 - origin.x, (goal.OriginY + goal.goal.Value.y) * 8 - origin.y, 8, 8);
            return scene;
        }
        int? Slot(Vector2Int p)
        {
            for (int slot = 0; slot < SlotCount; slot++) if (SlotIn(View, slot).Contains(p)) return slot;
            return null;
        }
        static void Dash(PixelFrame frame, int x, int y, int length, bool across, Color32 color, float amount)
        {
            for (int t = 0; t < length; t += 6)
            {
                int run = Math.Min(3, length - t);
                if (across) frame.Fill(x + t, y, run, 1, color, amount); else frame.Fill(x, y + t, 1, run, color, amount);
            }
        }
        // One side of a room's frame, a screen at a time: solid, or dashed where another room shares it (drawn once, by the earlier room).
        void DrawFrame(PixelFrame frame, EditRoom room, int ox, int oy, Color32 color, float amount)
        {
            int x = room.atX * PhaseWorld.ScreenWidth - ox, y = room.atY * PhaseWorld.ScreenHeight - oy, w = room.w * PhaseWorld.ScreenWidth, h = room.h * PhaseWorld.ScreenHeight;
            int index = rooms.IndexOf(room);
            void Side(int count, bool across, int fixedAt, int unit, Func<int, EditRoom> other)
            {
                for (int s = 0; s < count; s++)
                {
                    var o = other(s); int at = (across ? x : y) + s * unit;
                    if (o == null) { if (across) frame.Fill(at, fixedAt, unit, 1, color, amount); else frame.Fill(fixedAt, at, 1, unit, color, amount); }
                    else if (rooms.IndexOf(o) > index) Dash(frame, across ? at : fixedAt, across ? fixedAt : at, unit, across, color, amount);
                }
            }
            Side(room.w, true, y, PhaseWorld.ScreenWidth, s => RoomAtScreen(room.atX + s, room.atY - 1));
            Side(room.w, true, y + h - 1, PhaseWorld.ScreenWidth, s => RoomAtScreen(room.atX + s, room.atY + room.h));
            Side(room.h, false, x, PhaseWorld.ScreenHeight, s => RoomAtScreen(room.atX - 1, room.atY + s));
            Side(room.h, false, x + w - 1, PhaseWorld.ScreenHeight, s => RoomAtScreen(room.atX + room.w, room.atY + s));
        }
        // The screens the move tool is dragging the chosen rooms by.
        Vector2Int DragScreens => dragFrom is Vector2Int from
            ? new Vector2Int((int)Math.Round((Cursor.x - from.x) / (double)Columns), (int)Math.Round((Cursor.y - from.y) / (double)Rows)) : Vector2Int.zero;
        protected override void Render()
        {
            var origin = Origin; int ox = origin.x, oy = origin.y;
            PixelFrame frame;
            if (Overview) { frame = new PixelFrame(pixels.Texture); DrawOverview(frame); }
            else
            {
                pixels.Scroll(0, 0);
                pixels.Draw(Preview());
                frame = new PixelFrame(pixels.Texture);
                DrawWorld(frame, ox, oy);
            }
            DrawPanel(frame);
            if (flash > 0) frame.Warn();
            frame.Apply();
        }
        void DrawWorld(PixelFrame frame, int ox, int oy)
        {
            Color32 color = PhasePixels.SurfaceColor(Mask);
            // Screens without a room are dimmed, and every screen seam shows as a faint line.
            int sw = PhaseWorld.ScreenWidth, sh = PhaseWorld.ScreenHeight;
            for (int sy = FloorDiv(oy, sh); sy * sh - oy < frame.Height; sy++)
                for (int sx = FloorDiv(ox, sw); sx * sw - ox < frame.Width; sx++)
                    if (RoomAtScreen(sx, sy) == null) frame.Fill(sx * sw - ox, sy * sh - oy, sw, sh, PixelFrame.Ink, .78f);
            for (int sx = FloorDiv(ox, sw); sx * sw - ox < frame.Width; sx++) frame.Fill(sx * sw - ox, 0, 1, frame.Height, PixelFrame.Light, .12f);
            for (int sy = FloorDiv(oy, sh); sy * sh - oy < frame.Height; sy++) frame.Fill(0, sy * sh - oy, frame.Width, 1, PixelFrame.Light, .12f);
            var under = RoomAtCell(Cursor);
            foreach (var room in rooms)
            {
                bool chosen = selection.Contains(room) || room == under;
                DrawFrame(frame, room, ox, oy, chosen ? PixelFrame.Light : PixelFrame.Light, chosen ? 1f : .4f);
                if (flash > 0 && missing.Contains(room) && (int)(flash * 6) % 2 == 0) DrawFrame(frame, room, ox, oy, PixelFrame.Warning, 1f);
            }
            if (dragFrom != null)
            {
                var by = DragScreens; bool blocked = rooms.Any(o => !selection.Contains(o) && selection.Any(r => r.atX + by.x < o.atX + o.w && r.atX + by.x + r.w > o.atX && r.atY + by.y < o.atY + o.h && r.atY + by.y + r.h > o.atY));
                foreach (var room in selection)
                    frame.Outline((room.atX + by.x) * sw - ox, (room.atY + by.y) * sh - oy, room.w * sw, room.h * sh, blocked ? PixelFrame.Warning : PixelFrame.Selection);
            }
            int cx = Cursor.x * 8 - ox, cy = Cursor.y * 8 - oy;
            if (Tool != Erase && Tool != Spawn && Tool != RoomTool && Tool != MoveTool) frame.Fill(cx, cy, 8, 8, color, .35f);
            if (Turns(KindOf(Tool))) frame.Blit(toolIcons[Tool], cx, cy, color, 1, .9f);
            frame.Outline(cx - 1, cy - 1, 10, 10, PixelFrame.Light, .9f);
            DrawPaths(frame, ox, oy);
            bool blink = SpawnBlocked && (int)(flash * 6) % 2 == 0;
            foreach (var room in rooms)
                foreach (var spawn in room.spawns)
                {
                    int x = (room.OriginX + spawn.x) * 8 - ox, y = (room.OriginY + spawn.y) * 8 - oy;
                    frame.Blit(spawnIcon, x, y, blink ? PixelFrame.Warning : PixelFrame.Light, 1, .9f);
                    frame.Outline(x, y, 8, 11, blink ? PixelFrame.Warning : PixelFrame.Light, .35f);
                }
        }
        // The overview scales every room to a few pixels per screen, centered in the area the panel leaves.
        (int scale, int x, int y, int minX, int minY) OverviewLayout()
        {
            var panel = PanelIn(View);
            var area = Side(View) ? new RectInt(0, 0, panel.x, View.y) : new RectInt(0, 0, View.x, panel.y);
            if (rooms.Count == 0) return (1, 0, 0, 0, 0);
            int minX = rooms.Min(r => r.atX), minY = rooms.Min(r => r.atY), maxX = rooms.Max(r => r.atX + r.w), maxY = rooms.Max(r => r.atY + r.h);
            int scale = Math.Max(1, Math.Min(24, Math.Min((area.width - 8) / (maxX - minX), (area.height - 8) / (maxY - minY))));
            return (scale, area.x + (area.width - (maxX - minX) * scale) / 2, area.y + (area.height - (maxY - minY) * scale) / 2, minX, minY);
        }
        void DrawOverview(PixelFrame frame)
        {
            frame.Fill(0, 0, frame.Width, frame.Height, PixelFrame.Ink);
            if (rooms.Count == 0) return;
            var (scale, ox, oy, minX, minY) = OverviewLayout();
            foreach (var room in rooms)
            {
                int x = ox + (room.atX - minX) * scale, y = oy + (room.atY - minY) * scale;
                frame.Fill(x, y, room.w * scale, room.h * scale, PixelFrame.Light, .2f);
                frame.Outline(x, y, room.w * scale, room.h * scale, PixelFrame.Light, selection.Contains(room) ? 1f : .6f);
                foreach (var spawn in room.spawns) frame.Fill(x + spawn.x * scale / Columns, y + spawn.y * scale / Rows, Math.Max(1, scale / 8), Math.Max(1, scale / 8), PixelFrame.Selection);
                if (room.goal is Vector2Int g) frame.Fill(x + g.x * scale / Columns, y + g.y * scale / Rows, Math.Max(1, scale / 8), Math.Max(1, scale / 8), PhasePixels.Hex("f1dc81"));
            }
            frame.Outline(ox + (int)((Scroll.x / (double)Columns - minX) * scale), oy + (int)((Scroll.y / (double)Rows - minY) * scale), scale, scale, PixelFrame.Selection);
        }
        // Clicking the overview scrolls the window to the world cell under the pointer.
        bool ScrollToOverview(Vector2Int p)
        {
            var (scale, ox, oy, minX, minY) = OverviewLayout();
            if (rooms.Count == 0) return false;
            var cell = new Vector2Int(FloorDiv((p.x - ox) * Columns, scale) + minX * Columns, FloorDiv((p.y - oy) * Rows, scale) + minY * Rows);
            Overview = false; Cursor = cell;
            ScrollTo(cell.x - Columns / 2, cell.y - Rows / 2);
            return true;
        }
        void DrawPanel(PixelFrame frame)
        {
            Color32 color = PhasePixels.SurfaceColor(Mask);
            var panel = PanelIn(View);
            // The opaque panel hides the cursor outline and previews that reach past the grid's edge.
            frame.Fill(panel.x, panel.y, panel.width, panel.height, PixelFrame.Ink);
            for (int slot = 0; slot < SlotCount; slot++)
            {
                var cell = SlotIn(View, slot); int x = cell.x + 2, y = cell.y + 2;
                bool selected = slot < MaskSlot ? kinds[slot] == KindOf(Tool) : slot < ImportSlot && masks[slot - MaskSlot] == Mask;
                if (slot == Hover) frame.Fill(cell.x, cell.y, Pitch, Pitch, PixelFrame.Light, .2f);
                bool plain = kinds.Length > slot && slot < MaskSlot && (kinds[slot] == Spawn || kinds[slot] == Erase || kinds[slot] >= GoalTool);
                if (slot < MaskSlot) frame.Blit(toolIcons[ToolFor(kinds[slot])], x, y, plain ? PixelFrame.Light : color, 2, selected ? 1 : .45f);
                else if (slot < ImportSlot) frame.Fill(x, y, 14, 14, PhasePixels.SurfaceColor(masks[slot - MaskSlot]), selected ? 1 : .6f);
                else if (slot < BackSlot) frame.Blit(slot == ImportSlot ? importIcon : slot == ExportSlot ? exportIcon : PhaseMenu.PlayIcon, cell.x + 4, cell.y + 4, PixelFrame.Light);
                else frame.Blit(backIcon, x, y, PixelFrame.Light, 2);
                if (selected) frame.Outline(cell.x, cell.y, Pitch, Pitch, PixelFrame.Light);
            }
            if (SpawnBlocked && (int)(flash * 6) % 2 == 0)
            {
                var flag = SlotIn(View, Array.IndexOf(kinds, Spawn));
                frame.Outline(flag.x, flag.y, Pitch, Pitch, PixelFrame.Warning); frame.Outline(flag.x + 1, flag.y + 1, Pitch - 2, Pitch - 2, PixelFrame.Warning);
            }
        }
        // Every platform's stops are outlined and joined by dotted arrows in the order it visits them; the one being placed is drawn the same, still open.
        void DrawPaths(PixelFrame frame, int ox, int oy)
        {
            foreach (var room in rooms)
                foreach (var platform in room.platforms)
                    DrawPath(frame, ox, oy, platform.pivots.Select(p => new Vector2Int(p.x + room.OriginX, p.y + room.OriginY)).ToList(), platform.vertical, platform.length, platform.reverse, true, PhasePixels.SurfaceColor(platform.phaseMask));
            if (Placing) DrawPath(frame, ox, oy, draft.Select(c => c + new Vector2Int(draftRoom.OriginX, draftRoom.OriginY)).ToList(), draftVertical, Math.Max(1, draftLength), false, false, PhasePixels.SurfaceColor(Mask));
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
            if (slot < MaskSlot) { Select(kinds[slot]); dragFrom = null; }
            else if (slot < ImportSlot) Mask = masks[slot - MaskSlot];
            else if (slot == ImportSlot) RequestImport();
            else if (slot == ExportSlot) ExportDraft();
            else if (slot == PlaySlot) { StartPlaytest(); return; }
            else if (slot == BackSlot) { Leave(); return; }
            Refresh();
        }
        bool ScrollTo(int x, int y)
        {
            var next = new Vector2Int(x, y);
            if (next == Scroll) return false;
            Scroll = next; scene = null; return true;
        }
        // Scrolls just far enough that the cell sits at least `margin` cells inside the window.
        bool Reveal(Vector2Int cell, int margin = 2) => ScrollTo(
            Math.Max(Math.Min(Scroll.x, cell.x - margin), cell.x + margin + 1 - Columns), Math.Max(Math.Min(Scroll.y, cell.y - margin), cell.y + margin + 1 - Rows));
        // Every spawn refusal in PhaseCustomMap.Validate names 出生點 (the spawn).
        void Refuse(ArgumentException error)
        {
            flash = 1.2f; SpawnBlocked = error.Message.Contains("出生點");
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
            if (flash > 0) { flash -= Time.unscaledDeltaTime; SpawnBlocked &= flash > 0; if (flash <= 0) missing.Clear(); changed = true; }
            bool ctrl = k != null && (k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed);
            if (k != null)
            {
                if (k.tabKey.wasPressedThisFrame) { Overview = !Overview; dragFrom = null; changed = true; }
                if (k.escapeKey.wasPressedThisFrame)
                {
                    if (Overview) { Overview = false; Refresh(); return; }
                    if (Placing) { CancelPlacing(); Refresh(); } else Leave();
                    return;
                }
                if (!Overview && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)) { StartPlaytest(); return; }
            }
            if (k != null && !Overview)
            {
                for (int i = 0; i < masks.Length; i++) if (k[Key.Digit1 + i].wasPressedThisFrame) { Mask = masks[i]; changed = true; }
                // R/E turn the tool and X cycles the type, even when bound to an action; that action waits until they are released.
                if (k.rKey.wasPressedThisFrame) changed |= Rotate(1);
                if (k.eKey.wasPressedThisFrame) changed |= Rotate(-1);
                bool turning = k.rKey.isPressed || k.eKey.isPressed || k.xKey.isPressed || k.fKey.isPressed;
                if (k.fKey.wasPressedThisFrame && RoomAtCell(Cursor) is EditRoom there)
                {
                    cur = there;
                    if (PlatformAt(new Vector2Int(Cursor.x - there.OriginX, Cursor.y - there.OriginY), true).platform is DemoPlatform loop) { loop.reverse = !loop.reverse; changed |= Edited(); }
                }
                if (k.xKey.wasPressedThisFrame) { Select(kinds[(Array.IndexOf(kinds, KindOf(Tool)) + 1) % kinds.Length]); changed = true; }
                var input = turning ? default : PhaseSandbox.ReadControls(k);
                // Up and down are not bindable, so the cursor takes them from the arrow keys.
                var step = new Vector2Int(input.x, turning ? 0 : (k.downArrowKey.isPressed ? 1 : 0) - (k.upArrowKey.isPressed ? 1 : 0));
                if (step != Vector2Int.zero && (step != held || Time.unscaledTime >= repeatAt))
                {
                    repeatAt = Time.unscaledTime + (step != held ? .25f : .06f);
                    // Ctrl with an arrow moves the chosen rooms one screen.
                    if (Tool == MoveTool && ctrl && selection.Count > 0) { if (step != held) changed |= MoveSelection(step.x, step.y); }
                    else { Cursor += step; Reveal(Cursor); changed = true; }
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
                var cell = new Vector2Int((p.x + origin.x) >> 3, (p.y + origin.y) >> 3);
                bool onGrid = !PanelIn(View).Contains(p);
                if (slot != Hover) { Hover = slot; changed = true; }
                if (Overview)
                {
                    if (m.leftButton.wasPressedThisFrame && slot is int picked) { Choose(picked); return; }
                    if (m.leftButton.wasPressedThisFrame && onGrid) changed |= ScrollToOverview(p);
                    if (changed) Refresh();
                    return;
                }
                // The wheel scrolls rows (Ctrl or a sideways wheel for columns) and by dragging with the middle button, or the left one
                // under Ctrl. With a spike or laser tool the wheel turns it instead.
                bool pan = m.middleButton.wasPressedThisFrame || (ctrl && m.leftButton.wasPressedThisFrame);
                var wheel = m.scroll.ReadValue();
                if (wheel != Vector2.zero)
                {
                    int sideways = wheel.x != 0 ? Math.Sign(wheel.x) : ctrl ? -Math.Sign(wheel.y) : 0;
                    if (sideways != 0) changed |= ScrollTo(Scroll.x + sideways * 2, Scroll.y);
                    else if (Turns(KindOf(Tool))) changed |= Rotate(wheel.y < 0 ? 1 : -1);
                    else changed |= ScrollTo(Scroll.x, Scroll.y - Math.Sign(wheel.y) * 2);
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
                if (onGrid && p != lastMouse) { Cursor = cell; changed = true; }
                lastMouse = p;
                bool press = m.leftButton.wasPressedThisFrame;
                if (Tool == MoveTool)
                {
                    // Pressing on a room picks it and dragging carries the chosen rooms; letting go drops them by whole screens.
                    if (onGrid && stroke && press) { changed |= Pick(Cursor, Shift); dragFrom = RoomAtCell(Cursor) != null && selection.Count > 0 ? Cursor : (Vector2Int?)null; changed = true; }
                    if (dragFrom != null && !m.leftButton.isPressed) { var by = DragScreens; dragFrom = null; MoveSelection(by.x, by.y); changed = true; }
                }
                else if (onGrid && stroke && m.leftButton.isPressed) { painting = true; changed |= Apply(Cursor, Tool, press); }
                if (onGrid && stroke && m.rightButton.isPressed) { painting = true; changed |= Apply(Cursor, Erase); }
            }
            if (dirty && !painting) Save();
            if (changed) Refresh();
        }
        // A world that can be played or exported: it has rooms, each with a spawn, and passes the map file's own checks. A room without a spawn blinks.
        bool Check(out PhaseWorld world)
        {
            world = World(); missing.Clear();
            if (rooms.Count == 0) { flash = 1.2f; Refresh(); return false; }
            missing.AddRange(rooms.Where(r => r.spawns.Count == 0));
            if (missing.Count > 0) { flash = 1.2f; Refresh(); return false; }
            try { PhaseCustomMap.Validate(world); }
            catch (ArgumentException error) { Refuse(error); return false; }
            return true;
        }
        // Web saves and opens JSON files through the page; elsewhere the system clipboard carries the same JSON.
        public bool ExportDraft()
        {
            if (!Check(out var world)) return false;
            string json = PhaseCustomMap.ToJson(world);
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
            try { Fill(PhaseCustomMap.Parse(json)); }
            catch (ArgumentException) { flash = .4f; Refresh(); return; }
            Save(); Refresh();
        }
        // The playtest starts in the room under the middle of the window, at that room's spawn nearest to it.
        public bool StartPlaytest()
        {
            if (!Check(out var world)) return false;
            var middle = Scroll + new Vector2Int(Columns / 2, Rows / 2);
            double Distance(EditRoom r) => Math.Pow(Math.Max(0, Math.Max(r.OriginX - middle.x, middle.x - (r.OriginX + r.Cols - 1))), 2) + Math.Pow(Math.Max(0, Math.Max(r.OriginY - middle.y, middle.y - (r.OriginY + r.RowCount - 1))), 2);
            var room = rooms.OrderBy(Distance).First();
            Play(world, rooms.IndexOf(room), ((middle.x - room.OriginX) * 8.0, (middle.y - room.OriginY) * 8.0)); return true;
        }
        void Play(PhaseWorld world, int? start = null, (double x, double y)? near = null)
        {
            CancelPlacing();
            if (dirty) Save();
            surface.enabled = false;
            try { Playtest = PhaseSandbox.CreateCustom(world, pixelCamera, pixelMaterial, transform, start, near); }
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
