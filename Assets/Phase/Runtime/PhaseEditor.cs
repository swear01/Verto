using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Phase
{
    // Paints terrain, spikes and the spawn on the custom-map grid, then playtests it in place.
    public sealed class PhaseEditor : PhaseScreen
    {
        public const string Scene = "PhaseEditor", DraftKey = "phase.editor.v1";
        public const int Columns = 40, Rows = 22;
        // Cell tools: 0 empty, 1 terrain, 2–5 Up/Down/Left/Right spikes. Spawn only moves the player start.
        public const int Erase = 0, Terrain = 1, Spawn = 6;
        static readonly int[] toolOrder = { Terrain, 2, 3, 4, 5, Spawn, Erase }, masks = { 7, 1, 2, 4, 5, 3, 6 };
        static readonly string[] spikeUp = { ".......", "...#...", "..###..", "..###..", ".#####.", ".#####.", "#######" };
        static readonly string[][] toolIcons = {
            Enumerable.Repeat("#######", 7).ToArray(), spikeUp, spikeUp.Reverse().ToArray(),
            Turn(spikeUp), Turn(spikeUp).Select(r => new string(r.Reverse().ToArray())).ToArray(),
            new[] { "#......", "####...", "#####..", "####...", "#......", "#......", "#......" },
            new[] { "#.....#", ".#...#.", "..#.#..", "...#...", "..#.#..", ".#...#.", "#.....#" }
        };
        static readonly string[] backIcon = { "...#...", "..#....", ".#.....", "#######", ".#.....", "..#....", "...#..." };
        static readonly Color32 Warning = new Color32(255, 143, 145, 255);
        readonly byte[] tools = new byte[Columns * Rows], colors = new byte[Columns * Rows];
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
        protected override void Awake() { Load(); base.Awake(); }
        void Load()
        {
            string draft = PlayerPrefs.GetString(DraftKey, "");
            var parts = draft.Split(';');
            var spawn = parts[0].Split(',');
            if (parts.Length == 2 && parts[1].Length == tools.Length * 2 && spawn.Length == 2 &&
                int.TryParse(spawn[0], out int sx) && int.TryParse(spawn[1], out int sy) && sx >= 0 && sx < Columns && sy >= 0 && sy <= Rows - 2)
            {
                bool valid = true;
                for (int i = 0; i < tools.Length && valid; i++)
                {
                    int tool = parts[1][i * 2] - '0', mask = parts[1][i * 2 + 1] - '0';
                    valid = tool == Erase ? mask == 0 : tool > 0 && tool < Spawn && mask >= 1 && mask <= 7;
                    tools[i] = (byte)tool; colors[i] = (byte)mask;
                }
                if (valid) { SpawnCell = new Vector2Int(sx, sy); return; }
            }
            Array.Clear(tools, 0, tools.Length); Array.Clear(colors, 0, colors.Length);
            for (int x = 0; x < Columns; x++) { tools[(Rows - 1) * Columns + x] = Terrain; colors[(Rows - 1) * Columns + x] = 7; }
        }
        void Save()
        {
            var cells = new char[tools.Length * 2];
            for (int i = 0; i < tools.Length; i++) { cells[i * 2] = (char)('0' + tools[i]); cells[i * 2 + 1] = (char)('0' + colors[i]); }
            PlayerPrefs.SetString(DraftKey, $"{SpawnCell.x},{SpawnCell.y};{new string(cells)}");
            PlayerPrefs.Save(); dirty = false;
        }
        public PhaseDemoRoom Room()
        {
            var blocks = new List<Block>(); var spikes = new List<DemoSpike>();
            for (int i = 0; i < tools.Length; i++)
            {
                int x = i % Columns * 8, y = i / Columns * 8;
                if (tools[i] == Terrain) blocks.Add(new Block(x, y, 8, 8, -1, colors[i]));
                else if (tools[i] != Erase) spikes.Add(new DemoSpike { x = x, y = y, length = 8, direction = (SpikeDirection)(tools[i] - 2), phaseMask = colors[i] });
            }
            return new PhaseDemoRoom { name = "自訂地圖", blocks = blocks.ToArray(), spikes = spikes.ToArray(), spawnX = SpawnCell.x * 8, spawnY = SpawnCell.y * 8 };
        }
        bool Apply(Vector2Int cell, int tool)
        {
            if (tool == Spawn)
            {
                var spawn = new Vector2Int(cell.x, Math.Min(cell.y, Rows - 2));
                if (spawn == SpawnCell) return false;
                SpawnCell = spawn; return dirty = true;
            }
            int i = cell.y * Columns + cell.x; byte color = (byte)(tool == Erase ? 0 : Mask);
            if (tools[i] == tool && colors[i] == color) return false;
            tools[i] = (byte)tool; colors[i] = color; return dirty = true;
        }

        int BarTop => Cursor.y * 8 - pixels.OriginY < 24 ? pixels.Height - 16 : 3;
        static int SlotX(int slot) => 6 + slot * 11 + (slot >= 7 ? 4 : 0) + (slot >= 14 ? 4 : 0);
        int? Slot(Vector2Int p)
        {
            if (p.y < BarTop || p.y >= BarTop + 13) return null;
            for (int slot = 0; slot < 16; slot++) if (p.x >= SlotX(slot) - 2 && p.x < SlotX(slot) + 9) return slot;
            return null;
        }
        protected override void Render()
        {
            var room = Room();
            var view = new PhaseController(room.blocks, room.spawnX, room.spawnY, spikes: room.CreateSpikes());
            view.ResizeRoom(pixels.Width);
            pixels.Draw(view);
            var frame = new PixelFrame(pixels.Texture);
            Color32 color = PhasePixels.SurfaceColor(Mask);
            int cx = Cursor.x * 8 - pixels.OriginX, cy = Cursor.y * 8 - pixels.OriginY;
            if (Tool != Erase && Tool != Spawn) frame.Fill(cx, cy, 8, 8, color, .35f);
            frame.Outline(cx - 1, cy - 1, 10, 10, PixelFrame.Light, .9f);
            if (flash > 0) { frame.Outline(0, 0, frame.Width, frame.Height, Warning); frame.Outline(1, 1, frame.Width - 2, frame.Height - 2, Warning); }
            int top = BarTop;
            frame.Fill(2, top, SlotX(15) + 9, 13, PixelFrame.Ink, .85f);
            for (int slot = 0; slot < 16; slot++)
            {
                int x = SlotX(slot), y = top + 3;
                bool selected = slot < 7 ? toolOrder[slot] == Tool : slot < 14 && masks[slot - 7] == Mask;
                if (slot < 7) frame.Blit(toolIcons[slot], x, y, slot < 5 ? color : PixelFrame.Light, amount: selected ? 1 : .45f);
                else if (slot < 14) frame.Fill(x, y, 7, 7, PhasePixels.SurfaceColor(masks[slot - 7]), selected ? 1 : .6f);
                else frame.Blit(slot == 14 ? PhaseTitle.PlayIcon : backIcon, x, y, PixelFrame.Light);
                if (selected) frame.Outline(x - 2, y - 2, 11, 11, PixelFrame.Light);
            }
            frame.Apply();
        }
        void Choose(int slot)
        {
            if (slot < 7) Tool = toolOrder[slot];
            else if (slot < 14) Mask = masks[slot - 7];
            else if (slot == 14) { StartPlaytest(); return; }
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
                if (k.escapeKey.wasPressedThisFrame) { Leave(); return; }
                if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) { StartPlaytest(); return; }
                for (int i = 0; i < masks.Length; i++) if (k[Key.Digit1 + i].wasPressedThisFrame) { Mask = masks[i]; changed = true; }
                if (PhaseSandbox.Pressed(k, 5, true)) { Tool = toolOrder[(Array.IndexOf(toolOrder, Tool) + 1) % toolOrder.Length]; changed = true; }
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
                if (input.jumpHeld) changed |= Apply(Cursor, Tool);
                else if (input.grab) changed |= Apply(Cursor, Erase);
            }
            if (m != null)
            {
                var p = FramePoint(m); var slot = Slot(p);
                if (m.leftButton.wasPressedThisFrame) { barPress = slot != null; if (slot is int chosen) { Choose(chosen); return; } }
                int x = (p.x + pixels.OriginX) >> 3, y = (p.y + pixels.OriginY) >> 3;
                bool onGrid = slot == null && x >= 0 && x < Columns && y >= 0 && y < Rows;
                if (onGrid && p != lastMouse) { Cursor = new Vector2Int(x, y); changed = true; }
                lastMouse = p;
                if (onGrid && m.leftButton.isPressed && !barPress) { painting = true; changed |= Apply(Cursor, Tool); }
                else if (onGrid && m.rightButton.isPressed) { painting = true; changed |= Apply(Cursor, Erase); }
            }
            if (dirty && !painting) Save();
            if (changed) Refresh();
        }
        public bool StartPlaytest()
        {
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
        // The Web map editor's playtest runs here too and returns to this draft unchanged.
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
