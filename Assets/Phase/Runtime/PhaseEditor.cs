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
        public const int Columns = 40, Rows = 22;
        // Cell tools: 0 empty, 1 terrain, 2–5 Up/Down/Left/Right spikes, 7–10 Up/Down/Left/Right lasers,
        // 11 jump crystal, 12 dash crystal. Spawn only moves the player start. A platform is one 24×8 start plus the
        // one-way line behind it; a gear is still body then end. Both stay outside the cell grid.
        public const int Erase = 0, Terrain = 1, Spawn = 6, LaserTool = 7, CrystalTool = 11, LastTool = 12, PlatformTool = 13, GearTool = 14;
        static readonly int[] toolOrder = { Terrain, 2, 3, 4, 5, 7, 8, 9, 10, 11, 12, PlatformTool, GearTool, Spawn, Erase }, masks = { 7, 1, 2, 4, 5, 3, 6 };
        const int MaskSlot = 15, ImportSlot = 22, ExportSlot = 23, PlaySlot = 24, BackSlot = 25, SlotCount = 26, PlatformWidth = 24;
        static readonly string[] spikeUp = { ".......", "...#...", "..###..", "..###..", ".#####.", ".#####.", "#######" },
            laserUp = { "...#...", "...#...", "...#...", ".#####.", ".#...#.", ".#.#.#.", ".#####." },
            arrowDown = { "...#...", "...#...", "...#...", "#..#..#", ".#.#.#.", "..###..", "...#..." };
        static readonly string[][] toolIcons = {
            Enumerable.Repeat("#######", 7).ToArray(), spikeUp, spikeUp.Reverse().ToArray(), Turn(spikeUp), Mirror(Turn(spikeUp)),
            laserUp, laserUp.Reverse().ToArray(), Turn(laserUp), Mirror(Turn(laserUp)),
            new[] { "..###..", ".#####.", "##.####", "#######", "#######", ".#####.", "..###.." },
            new[] { ".......", ".#####.", ".#.###.", ".#####.", ".#####.", ".#####.", "......." },
            new[] { ".....#.", "#######", ".....#.", ".......", "#######", "#.#.#.#", "#######" },
            new[] { "...#...", ".#####.", ".##.##.", "##...##", ".##.##.", ".#####.", "...#..." },
            new[] { "#......", "####...", "#####..", "####...", "#......", "#......", "#......" },
            new[] { "#.....#", ".#...#.", "..#.#..", "...#...", "..#.#..", ".#...#.", "#.....#" }
        };
        static readonly string[] backIcon = { "...#...", "..#....", ".#.....", "#######", ".#.....", "..#....", "...#..." },
            importIcon = arrowDown.Reverse().ToArray();
        readonly byte[] tools = new byte[Columns * Rows], colors = new byte[Columns * Rows];
        readonly List<DemoPlatform> platforms = new List<DemoPlatform>();
        readonly List<DemoGear> gears = new List<DemoGear>();
        // The placed origin while a platform line or a gear wall is still open.
        public Vector2Int? Anchor { get; private set; }
        DemoPlatform line;
        public int Tool { get; private set; } = Terrain;
        public int Mask { get; private set; } = 7;
        public Vector2Int Cursor { get; private set; } = new Vector2Int(8, 18);
        public Vector2Int SpawnCell { get; private set; } = new Vector2Int(3, 19);
        public PhaseSandbox Playtest { get; private set; }
        protected override bool Hidden => Playtest != null;
        Vector2Int held, lastMouse = new Vector2Int(-1, -1);
        float repeatAt, flash;
        bool dirty, barPress;

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
            if (parts.Length == 2 && parts[1].Length >= tools.Length * 2 && spawn.Length == 2 &&
                int.TryParse(spawn[0], out int sx) && int.TryParse(spawn[1], out int sy) && sx >= 0 && sx < Columns && sy >= 0 && sy <= Rows - 2)
            {
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
            Array.Clear(tools, 0, tools.Length); Array.Clear(colors, 0, colors.Length); platforms.Clear(); gears.Clear();
            for (int x = 0; x < Columns; x++) { tools[(Rows - 1) * Columns + x] = Terrain; colors[(Rows - 1) * Columns + x] = 7; }
        }
        void Save()
        {
            var cells = new char[tools.Length * 2];
            for (int i = 0; i < tools.Length; i++) { cells[i * 2] = (char)('0' + tools[i]); cells[i * 2 + 1] = (char)('0' + colors[i]); }
            PlayerPrefs.SetString(DraftKey, $"{SpawnCell.x},{SpawnCell.y};{new string(cells)}{PhaseCustomMap.MechanismJson(Room())}");
            PlayerPrefs.Save(); dirty = false;
        }
        public PhaseDemoRoom Room()
        {
            var blocks = new List<Block>(); var spikes = new List<DemoSpike>(); var lasers = new List<DemoLaser>(); var crystals = new List<DemoCrystal>();
            for (int i = 0; i < tools.Length; i++)
            {
                int x = i % Columns * 8, y = i / Columns * 8, tool = tools[i];
                if (tool == Terrain) blocks.Add(new Block(x, y, 8, 8, -1, colors[i]));
                else if (tool >= CrystalTool) crystals.Add(new DemoCrystal { x = x, y = y, kind = (CrystalKind)(tool - CrystalTool), phaseMask = colors[i] });
                else if (tool >= LaserTool) lasers.Add(new DemoLaser { x = x, y = y, direction = (LaserDirection)(tool - LaserTool), phaseMask = colors[i] });
                else if (tool != Erase) spikes.Add(new DemoSpike { x = x, y = y, length = 8, direction = (SpikeDirection)(tool - 2), phaseMask = colors[i] });
            }
            return new PhaseDemoRoom { name = "自訂地圖", blocks = blocks.ToArray(), spikes = spikes.ToArray(), lasers = lasers.ToArray(), crystals = crystals.ToArray(),
                platforms = platforms.ToArray(), gears = gears.ToArray(), spawnX = SpawnCell.x * 8, spawnY = SpawnCell.y * 8 };
        }
        static bool IsMechanism(int tool) => tool >= PlatformTool;
        static RectInt Box(Block b) => new RectInt((int)b.x, (int)b.y, (int)b.w, (int)b.h);
        static RectInt Box(DemoPlatform p) => new RectInt((int)p.nodeX[0], (int)p.nodeY[0], (int)p.w, (int)p.h);
        static RectInt Box(Vector2Int cell, int w = 8, int h = 8) => new RectInt(cell.x * 8, cell.y * 8, w, h);
        IEnumerable<RectInt> Bodies() => platforms.Select(Box).Concat(gears.SelectMany(g => new[] { Box(g.wall), Box(g.platform) }));
        bool Free(RectInt area)
        {
            if (area.xMin < 0 || area.yMin < 0 || area.xMax > Columns * 8 || area.yMax > Rows * 8 || Bodies().Any(b => b.Overlaps(area))) return false;
            for (int y = area.yMin / 8; y < area.yMax / 8; y++) for (int x = area.xMin / 8; x < area.xMax / 8; x++) if (tools[y * Columns + x] != Erase) return false;
            return true;
        }
        // The 24×8 body stays inside the map. Stops keep both axes, so a segment may be diagonal.
        static Vector2Int Fit(Vector2Int cell) { cell.x = Math.Min(cell.x, Columns - PlatformWidth / 8); return cell; }
        static RectInt Stop(DemoPlatform platform, int i) => new RectInt((int)platform.nodeX[i], (int)platform.nodeY[i], (int)platform.w, (int)platform.h);
        static void AddStop(DemoPlatform platform, Vector2Int cell)
        {
            int n = platform.nodeX.Length;
            Array.Resize(ref platform.nodeX, n + 1); Array.Resize(ref platform.nodeY, n + 1);
            platform.nodeX[n] = cell.x * 8; platform.nodeY[n] = cell.y * 8;
        }
        // An 8×56 wall at the anchor drives a 56×8 bridge two cells to its right on the chosen row. The end column sets
        // equal wall and bridge travel of 8–24 px, which keeps the bridge rack over the output pinion.
        static DemoGear GearAt(Vector2Int wall, Vector2Int cell)
        {
            int x = wall.x * 8, y = wall.y * 8, startX = x + 16;
            int travel = Mathf.Clamp(cell.x * 8 - startX, 8, Math.Min(24, Math.Min(y, Columns * 8 - 56 - startX)));
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
                    gears.Add(gear); Anchor = null; return dirty = true;
                }
                if (cell.y >= 1 && cell.x * 8 + 80 <= Columns * 8 && Free(Box(cell, 8, 56))) Anchor = cell; else flash = .4f;
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
                return dirty = true;
            }
            if (!Free(Box(cell, PlatformWidth))) { flash = .4f; return true; }
            Anchor = cell; line = null; return true;
        }
        bool Cancel()
        {
            if (Anchor == null) return false;
            Anchor = null; line = null; return true;
        }
        void Select(int tool) { Tool = tool; Anchor = null; line = null; }
        bool Apply(Vector2Int cell, int tool)
        {
            if (tool == Spawn)
            {
                var spawn = new Vector2Int(cell.x, Math.Min(cell.y, Rows - 2));
                if (spawn == SpawnCell) return false;
                SpawnCell = spawn; return dirty = true;
            }
            var area = Box(cell);
            if (tool == Erase && platforms.RemoveAll(p => Box(p).Overlaps(area)) + gears.RemoveAll(g => Box(g.wall).Overlaps(area) || Box(g.platform).Overlaps(area)) > 0)
            {
                if (line != null && !platforms.Contains(line)) { line = null; Anchor = null; }
                return dirty = true;
            }
            if (tool != Erase && Bodies().Any(b => b.Overlaps(area))) return false;
            int i = cell.y * Columns + cell.x; byte color = (byte)(tool == Erase ? 0 : Mask);
            if (tools[i] == tool && colors[i] == color) return false;
            tools[i] = (byte)tool; colors[i] = color; return dirty = true;
        }

        // Two rows (tools; colors, files, play, back) stay left of the Web page's top-right buttons.
        int BarTop => Cursor.y * 8 - pixels.OriginY < 32 ? pixels.Height - 29 : 3;
        static int SlotX(int slot) => slot < MaskSlot ? 6 + slot * 11 : 6 + (slot - MaskSlot) * 11 + (slot >= ImportSlot ? 4 : 0) + (slot >= PlaySlot ? 4 : 0);
        int SlotY(int slot) => BarTop + 3 + (slot < MaskSlot ? 0 : 12);
        int? Slot(Vector2Int p)
        {
            for (int slot = 0; slot < SlotCount; slot++)
                if (p.x >= SlotX(slot) - 2 && p.x < SlotX(slot) + 9 && p.y >= SlotY(slot) - 2 && p.y < SlotY(slot) + 9) return slot;
            return null;
        }
        protected override void Render()
        {
            var room = Room();
            var view = new PhaseController(room.blocks, room.spawnX, room.spawnY, room.CreatePlatforms(), room.CreateGears(), room.CreateSpikes(), room.CreateLasers(), room.CreateCrystals());
            view.ResizeRoom(pixels.Width);
            pixels.Draw(view);
            var frame = new PixelFrame(pixels.Texture);
            Color32 color = PhasePixels.SurfaceColor(Mask);
            int cx = Cursor.x * 8 - pixels.OriginX, cy = Cursor.y * 8 - pixels.OriginY;
            int ox = pixels.OriginX, oy = pixels.OriginY;
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
            frame.Outline(cx - 1, cy - 1, 10, 10, PixelFrame.Light, .9f);
            if (flash > 0) frame.Warn();
            frame.Fill(2, BarTop, SlotX(MaskSlot - 1) + 9, 26, PixelFrame.Ink, .85f);
            for (int slot = 0; slot < SlotCount; slot++)
            {
                int x = SlotX(slot), y = SlotY(slot);
                bool selected = slot < MaskSlot ? toolOrder[slot] == Tool : slot < ImportSlot && masks[slot - MaskSlot] == Mask;
                if (slot < MaskSlot) frame.Blit(toolIcons[slot], x, y, toolOrder[slot] == Spawn || toolOrder[slot] == Erase || IsMechanism(toolOrder[slot]) ? PixelFrame.Light : color, amount: selected ? 1 : .45f);
                else if (slot < ImportSlot) frame.Fill(x, y, 7, 7, PhasePixels.SurfaceColor(masks[slot - MaskSlot]), selected ? 1 : .6f);
                else frame.Blit(slot == ImportSlot ? importIcon : slot == ExportSlot ? arrowDown : slot == PlaySlot ? PhaseMenu.PlayIcon : backIcon, x, y, PixelFrame.Light);
                if (selected) frame.Outline(x - 2, y - 2, 11, 11, PixelFrame.Light);
            }
            frame.Apply();
        }
        void Choose(int slot)
        {
            if (slot < MaskSlot) Select(toolOrder[slot]);
            else if (slot < ImportSlot) Mask = masks[slot - MaskSlot];
            else if (slot == ImportSlot) { RequestImport(); return; }
            else if (slot == ExportSlot) { ExportDraft(); return; }
            else if (slot == PlaySlot) { StartPlaytest(); return; }
            else { Leave(); return; }
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
            if (flash > 0) { flash -= Time.unscaledDeltaTime; changed = true; }
            if (k != null)
            {
                if (k.escapeKey.wasPressedThisFrame) { if (!Cancel()) { Leave(); return; } changed = true; }
                if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) { StartPlaytest(); return; }
                for (int i = 0; i < masks.Length; i++) if (k[Key.Digit1 + i].wasPressedThisFrame) { Mask = masks[i]; changed = true; }
                if (PhaseSandbox.Pressed(k, 5, true)) { Select(toolOrder[(Array.IndexOf(toolOrder, Tool) + 1) % toolOrder.Length]); changed = true; }
                var input = PhaseSandbox.ReadControls(k);
                var step = new Vector2Int(input.x, input.y);
                if (step != Vector2Int.zero && (step != held || Time.unscaledTime >= repeatAt))
                {
                    repeatAt = Time.unscaledTime + (step != held ? .25f : .06f);
                    Cursor = new Vector2Int(Math.Max(0, Math.Min(Columns - 1, Cursor.x + step.x)), Math.Max(0, Math.Min(Rows - 1, Cursor.y + step.y)));
                    changed = true;
                }
                held = step;
                painting = input.jumpHeld || input.grab;
                if (IsMechanism(Tool) && input.jumpHeld) { if (PhaseSandbox.Pressed(k, 4, true)) changed |= Place(Cursor); }
                else if (input.jumpHeld) changed |= Apply(Cursor, Tool);
                else if (input.grab) changed |= Cancel() || Apply(Cursor, Erase);
            }
            if (m != null)
            {
                var p = FramePoint(m); var slot = Slot(p);
                if (m.leftButton.wasPressedThisFrame) { barPress = slot != null; if (slot is int chosen) { Choose(chosen); return; } }
                int x = (p.x + pixels.OriginX) >> 3, y = (p.y + pixels.OriginY) >> 3;
                bool onGrid = slot == null && x >= 0 && x < Columns && y >= 0 && y < Rows;
                if (onGrid && p != lastMouse) { Cursor = new Vector2Int(x, y); changed = true; }
                lastMouse = p;
                if (onGrid && m.leftButton.isPressed && !barPress)
                {
                    if (!IsMechanism(Tool)) { painting = true; changed |= Apply(Cursor, Tool); }
                    else if (m.leftButton.wasPressedThisFrame) changed |= Place(Cursor);
                }
                else if (onGrid && m.rightButton.isPressed) { painting = true; changed |= Cancel() || Apply(Cursor, Erase); }
            }
            if (dirty && !painting) Save();
            if (changed) Refresh();
        }
        // Web saves and opens JSON files through the page; elsewhere the system clipboard carries the same JSON.
        public bool ExportDraft()
        {
            var room = Room();
            try { PhaseCustomMap.Validate(room); }
            catch (ArgumentException) { flash = .4f; Refresh(); return false; }
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
            Array.Clear(tools, 0, tools.Length); Array.Clear(colors, 0, colors.Length); Anchor = null; line = null;
            platforms.Clear(); platforms.AddRange(room.platforms ?? Array.Empty<DemoPlatform>());
            gears.Clear(); gears.AddRange(room.gears ?? Array.Empty<DemoGear>());
            void Put(double x, double y, double w, double h, int tool, int mask)
            {
                for (int cy = (int)y / 8; cy < (y + h) / 8; cy++)
                    for (int cx = (int)x / 8; cx < (x + w) / 8; cx++) { tools[cy * Columns + cx] = (byte)tool; colors[cy * Columns + cx] = (byte)mask; }
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
            catch (ArgumentException) { flash = .4f; Refresh(); return false; }
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
