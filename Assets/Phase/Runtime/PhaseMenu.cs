using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Phase
{
    // A centered row of icons: arrows or movement keys choose, Enter or jump confirms, or click an icon.
    public sealed class PhaseMenu
    {
        public static readonly string[] PlayIcon = { "#......", "###....", "#####..", "#######", "#####..", "###....", "#......" },
            KeysIcon = { "#########", "#.#.#.#.#", "#########", "##.#.#.##", "#########", "##.....##", "#########" },
            LevelsIcon = { "###.###", "#.#.#.#", "###.###", ".......", "###.###", "#.#.#.#", "###.###" },
            HomeIcon = { "...#...", "..###..", ".#####.", "#######", ".#...#.", ".#.#.#.", ".#.#.#." },
            RetryIcon = { "..####.#.", ".#....##.", "#....###.", "#........", "#.......#", "#.......#", ".#.....#.", "..#####.." };
        readonly string[][] icons;
        public int Choice { get; private set; }
        int lastX;
        public PhaseMenu(params string[][] icons) { this.icons = icons; }

        int Left(int width, int i) => width / 2 + (i * 2 - icons.Length + 1) * 8 - icons[i][0].Length / 2;
        public void Draw(PixelFrame frame, int top)
        {
            for (int i = 0; i < icons.Length; i++) frame.Blit(icons[i], Left(frame.Width, i), top, PixelFrame.Light, amount: i == Choice ? 1 : .35f);
        }
        // Returns the confirmed icon, or -1; `changed` reports a new selection.
        public int Tick(Keyboard k, Mouse m, Vector2Int frameSize, int top, out bool changed)
        {
            changed = false;
            int x = k == null ? 0 : PhaseSandbox.ReadControls(k).x + (k.rightArrowKey.isPressed ? 1 : 0) - (k.leftArrowKey.isPressed ? 1 : 0);
            x = System.Math.Sign(x);
            if (x != 0 && x != lastX && Choice + x >= 0 && Choice + x < icons.Length) { Choice += x; changed = true; }
            lastX = x;
            if (m != null && m.leftButton.wasPressedThisFrame)
            {
                var p = m.position.ReadValue();
                int px = Mathf.FloorToInt(p.x / Screen.width * frameSize.x), py = Mathf.FloorToInt((1 - p.y / Screen.height) * frameSize.y);
                for (int i = 0; i < icons.Length; i++)
                    if (px >= Left(frameSize.x, i) - 3 && px < Left(frameSize.x, i) + icons[i][0].Length + 3 && py >= top - 3 && py < top + 10) { Choice = i; return i; }
            }
            return Confirmed(k) ? Choice : -1;
        }
        public static bool Confirmed(Keyboard k) =>
            k != null && (PhaseSandbox.Pressed(k, 4, true) || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
    }

    // Level select: every level is always available. Arrows or movement keys move, Enter or jump loads, Escape goes back.
    public sealed class PhaseLevels
    {
        const int Columns = 4, TileWidth = 17, TileHeight = 11, Gap = 4;
        public int Choice { get; private set; }
        Vector2Int last;
        public PhaseLevels(int current = 0) { Choice = Mathf.Clamp(current, 0, PhaseSandbox.Levels.Length - 1); }
        static Vector2Int Tile(Vector2Int frame, int i) => new Vector2Int(
            frame.x / 2 - (Columns * (TileWidth + Gap) - Gap) / 2 + i % Columns * (TileWidth + Gap),
            frame.y / 2 - ((PhaseSandbox.Levels.Length + Columns - 1) / Columns * (TileHeight + Gap) - Gap) / 2 + i / Columns * (TileHeight + Gap));
        // Returns the chosen level number, 0 when Escape goes back, or -1.
        public int Tick(Keyboard k, Mouse m, Vector2Int frameSize)
        {
            if (k == null) return -1;
            if (k.escapeKey.wasPressedThisFrame) return 0;
            var input = PhaseSandbox.ReadControls(k);
            var step = new Vector2Int(System.Math.Sign(input.x + (k.rightArrowKey.isPressed ? 1 : 0) - (k.leftArrowKey.isPressed ? 1 : 0)),
                System.Math.Sign(input.y + (k.downArrowKey.isPressed ? 1 : 0) - (k.upArrowKey.isPressed ? 1 : 0)));
            int column = Choice % Columns + (step.x != last.x ? step.x : 0), row = Choice / Columns + (step.y != last.y ? step.y : 0);
            if (column >= 0 && column < Columns && row >= 0 && row * Columns + column < PhaseSandbox.Levels.Length) Choice = row * Columns + column;
            last = step;
            if (m != null && m.leftButton.wasPressedThisFrame)
            {
                var p = m.position.ReadValue();
                var at = new Vector2Int(Mathf.FloorToInt(p.x / Screen.width * frameSize.x), Mathf.FloorToInt((1 - p.y / Screen.height) * frameSize.y));
                for (int i = 0; i < PhaseSandbox.Levels.Length; i++)
                {
                    var tile = Tile(frameSize, i);
                    if (at.x >= tile.x && at.x < tile.x + TileWidth && at.y >= tile.y && at.y < tile.y + TileHeight) { Choice = i; return i + 1; }
                }
            }
            return PhaseMenu.Confirmed(k) ? Choice + 1 : -1;
        }
        public void Draw(PixelFrame frame)
        {
            frame.Fill(0, 0, frame.Width, frame.Height, PixelFrame.Ink, .85f);
            for (int i = 0; i < PhaseSandbox.Levels.Length; i++)
            {
                var tile = Tile(new Vector2Int(frame.Width, frame.Height), i); string label = (i + 1).ToString();
                frame.Outline(tile.x, tile.y, TileWidth, TileHeight, PixelFrame.Light, i == Choice ? 1 : .3f);
                frame.Text(label, tile.x + (TileWidth - PixelFrame.TextWidth(label) + 1) / 2, tile.y + 3, i == Choice ? 1 : .6f);
            }
        }
    }

    // Key binding settings: seven action rows with two key slots each, then restore defaults.
    // Arrows move, Enter captures the next key, Backspace clears, Escape cancels or closes.
    public sealed class PhaseKeys
    {
        public const int RestoreRow = 7;
        static readonly string[] arrowLeft = { "...#...", "..#....", ".#.....", "#######", ".#.....", "..#....", "...#..." },
            arrowDown = { "...#...", "...#...", "...#...", "#..#..#", ".#.#.#.", "..###..", "...#..." };
        static readonly string[][] actionIcons = {
            arrowLeft, arrowLeft.Select(r => new string(r.Reverse().ToArray())).ToArray(), arrowDown.Reverse().ToArray(), arrowDown,
            new[] { "...#...", "..#.#..", ".#...#.", "...#...", "..#.#..", ".#...#.", "#######" },
            new[] { "#..#...", ".#..#..", "..#..#.", "...#..#", "..#..#.", ".#..#..", "#..#..." },
            new[] { ".....##", ".##..##", "#..#.##", "#..####", "#..#.##", ".##..##", ".....##" }
        };
        // Bindable key names (Unity Key names, plus Shift for both Shift keys) and their key-cap labels.
        public static readonly Dictionary<string, string> Labels = new Dictionary<string, string> {
            ["LeftArrow"] = "←", ["RightArrow"] = "→", ["UpArrow"] = "↑", ["DownArrow"] = "↓",
            ["Space"] = "SPACE", ["Shift"] = "SHIFT", ["Enter"] = "ENTER", ["Backspace"] = "BKSP",
            ["Comma"] = ",", ["Period"] = ".", ["Slash"] = "/", ["Semicolon"] = ";", ["Quote"] = "'", ["LeftBracket"] = "[",
            ["RightBracket"] = "]", ["Backslash"] = "\\", ["Backquote"] = "`", ["Minus"] = "-", ["Equals"] = "="
        };
        static PhaseKeys()
        {
            for (char c = 'A'; c <= 'Z'; c++) Labels[c.ToString()] = c.ToString();
            for (char c = '0'; c <= '9'; c++) Labels["Digit" + c] = c.ToString();
        }
        const int CapWidth = 25, Pitch = 11;
        public int Row { get; private set; }
        public int Column { get; private set; }
        public bool Capturing { get; private set; }
        public bool Flashing => flash > 0;
        float flash;

        // Returns true when Escape closes the screen.
        public bool Tick(Keyboard k)
        {
            if (flash > 0) flash -= Time.unscaledDeltaTime;
            if (k == null) return false;
            if (Capturing)
            {
                if (k.escapeKey.wasPressedThisFrame) Capturing = false;
                else if (k.backspaceKey.wasPressedThisFrame) Set("");
                else if (k.allKeys.FirstOrDefault(key => key.wasPressedThisFrame) is { } pressed)
                    Set(pressed.keyCode == Key.LeftShift || pressed.keyCode == Key.RightShift ? "Shift" : pressed.keyCode.ToString());
                return false;
            }
            if (k.escapeKey.wasPressedThisFrame) return true;
            if (k.upArrowKey.wasPressedThisFrame) Row = Mathf.Max(0, Row - 1);
            if (k.downArrowKey.wasPressedThisFrame) Row = Mathf.Min(RestoreRow, Row + 1);
            if (k.leftArrowKey.wasPressedThisFrame) Column = 0;
            if (k.rightArrowKey.wasPressedThisFrame) Column = 1;
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)
            {
                if (Row == RestoreRow) { PhaseSandbox.ApplyControls(PhaseSandbox.DefaultSlots); PhaseSandbox.SaveControls(); }
                else Capturing = true;
            }
            else if (k.backspaceKey.wasPressedThisFrame && Row < RestoreRow) Set("");
            return false;
        }
        // An invalid binding (unsupported key, duplicate, or an action left without keys) flashes and keeps the old map.
        void Set(string name)
        {
            Capturing = false;
            var next = PhaseSandbox.Slots.ToArray(); next[Row * 2 + Column] = name;
            if (PhaseSandbox.ApplyControls(next)) PhaseSandbox.SaveControls();
            else flash = .4f;
        }
        public void Draw(PixelFrame frame)
        {
            frame.Fill(0, 0, frame.Width, frame.Height, PixelFrame.Ink, .85f);
            if (Flashing) frame.Warn();
            int left = frame.Width / 2 - 34, top = frame.Height / 2 - 46;
            var slots = PhaseSandbox.Slots;
            for (int row = 0; row < actionIcons.Length; row++)
            {
                int y = top + row * Pitch;
                frame.Blit(actionIcons[row], left, y + 1, PixelFrame.Light);
                for (int column = 0; column < 2; column++)
                {
                    int x = left + 13 + column * (CapWidth + 4);
                    bool selected = row == Row && column == Column;
                    if (selected && Capturing && Time.unscaledTime * 4 % 2 < 1) frame.Fill(x, y, CapWidth, 9, PixelFrame.Light, .5f);
                    frame.Outline(x, y, CapWidth, 9, PixelFrame.Light, selected ? 1 : .3f);
                    string label = slots[row * 2 + column] == "" ? "" : Labels[slots[row * 2 + column]];
                    frame.Text(label, x + (CapWidth - PixelFrame.TextWidth(label) + 1) / 2, y + 2);
                }
            }
            int restoreY = top + RestoreRow * Pitch + 3, restoreX = frame.Width / 2 - 4;
            frame.Blit(PhaseMenu.RetryIcon, restoreX, restoreY, PixelFrame.Light, amount: Row == RestoreRow ? 1 : .35f);
            if (Row == RestoreRow) frame.Outline(restoreX - 2, restoreY - 2, 13, 12, PixelFrame.Light);
        }
    }
}
