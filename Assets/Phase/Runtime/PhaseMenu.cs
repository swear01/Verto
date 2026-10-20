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
            KeysIcon = { "..#.#..", ".#####.", "###.###", ".#...#.", "###.###", ".#####.", "..#.#.." },
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
                var p = FramePoint(m, frameSize);
                for (int i = 0; i < icons.Length; i++)
                    if (p.x >= Left(frameSize.x, i) - 3 && p.x < Left(frameSize.x, i) + icons[i][0].Length + 3 && p.y >= top - 3 && p.y < top + 10) { Choice = i; return i; }
            }
            return Confirmed(k) ? Choice : -1;
        }
        // The mouse position in frame pixels from the top-left.
        public static Vector2Int FramePoint(Mouse m, Vector2Int frameSize)
        {
            var p = m.position.ReadValue();
            return new Vector2Int(Mathf.FloorToInt(p.x / Screen.width * frameSize.x), Mathf.FloorToInt((1 - p.y / Screen.height) * frameSize.y));
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
                var at = PhaseMenu.FramePoint(m, frameSize);
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

    // Settings: seven action rows with two key slots each, restore defaults, then the fullscreen switch.
    // Arrows move; Enter, jump or a click captures the next key, restores or toggles; Backspace clears; Escape cancels or closes.
    public sealed class PhaseKeys
    {
        public const int RestoreRow = 7, FullScreenRow = 8;
        static readonly string[] fullScreenIcon = { "##...##", "#.....#", ".......", ".......", ".......", "#.....#", "##...##" };
        // Unity applies Screen.fullScreen natively and, within the confirming key or click, on Web; the Editor ignores it, so tests swap these.
        public static System.Func<bool> IsFullScreen = () => Screen.fullScreen;
        public static System.Action<bool> SetFullScreen = on => Screen.fullScreen = on;
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
        Vector2Int size;

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
            if (k.downArrowKey.wasPressedThisFrame) Row = Mathf.Min(FullScreenRow, Row + 1);
            if (k.leftArrowKey.wasPressedThisFrame) Column = 0;
            if (k.rightArrowKey.wasPressedThisFrame) Column = 1;
            var m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame)
            {
                var at = PhaseMenu.FramePoint(m, size);
                for (int row = 0; row <= FullScreenRow; row++)
                    for (int column = 0; column < 2; column++)
                        if (Area(size, row, column).Contains(at)) { Row = row; Column = column; Confirm(); return false; }
            }
            else if (PhaseMenu.Confirmed(k)) Confirm();
            else if (k.backspaceKey.wasPressedThisFrame && Row < RestoreRow) Set("");
            return false;
        }
        void Confirm()
        {
            if (Row == FullScreenRow) SetFullScreen(!IsFullScreen());
            else if (Row == RestoreRow) { PhaseSandbox.ApplyControls(PhaseSandbox.DefaultSlots); PhaseSandbox.SaveControls(); }
            else Capturing = true;
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
            size = new Vector2Int(frame.Width, frame.Height);
            frame.Fill(0, 0, frame.Width, frame.Height, PixelFrame.Ink, .85f);
            if (Flashing) frame.Warn();
            var slots = PhaseSandbox.Slots;
            for (int row = 0; row < actionIcons.Length; row++)
            {
                var icon = Area(size, row, 0);
                frame.Blit(actionIcons[row], icon.x - 13, icon.y + 1, PixelFrame.Light);
                for (int column = 0; column < 2; column++)
                {
                    var cap = Area(size, row, column);
                    bool selected = row == Row && column == Column;
                    if (selected && Capturing && Time.unscaledTime * 4 % 2 < 1) frame.Fill(cap.x, cap.y, cap.width, cap.height, PixelFrame.Light, .5f);
                    frame.Outline(cap.x, cap.y, cap.width, cap.height, PixelFrame.Light, selected ? 1 : .3f);
                    string label = slots[row * 2 + column] == "" ? "" : Labels[slots[row * 2 + column]];
                    frame.Text(label, cap.x + (CapWidth - PixelFrame.TextWidth(label) + 1) / 2, cap.y + 2);
                }
            }
            var restore = Area(size, RestoreRow, 0);
            frame.Blit(PhaseMenu.RetryIcon, restore.x + 2, restore.y + 2, PixelFrame.Light, amount: Row == RestoreRow ? 1 : .35f);
            if (Row == RestoreRow) frame.Outline(restore.x, restore.y, restore.width, restore.height, PixelFrame.Light);
            // Screen corners, then a switch whose knob sits right on a lit track when fullscreen.
            var full = Area(size, FullScreenRow, 0); bool on = IsFullScreen(); float amount = Row == FullScreenRow ? 1 : .35f;
            frame.Blit(fullScreenIcon, full.x + 2, full.y + 2, PixelFrame.Light, amount: amount);
            frame.Outline(full.x + 12, full.y + 2, 13, 7, PixelFrame.Light, amount);
            if (on) frame.Fill(full.x + 13, full.y + 3, 11, 5, PixelFrame.Light, amount * .35f);
            frame.Fill(full.x + (on ? 19 : 14), full.y + 4, 4, 3, PixelFrame.Light, amount);
            if (Row == FullScreenRow) frame.Outline(full.x, full.y, full.width, full.height, PixelFrame.Light);
        }
        // A key cap, or the restore and fullscreen controls with their selection border, in frame pixels.
        public static RectInt Area(Vector2Int frame, int row, int column)
        {
            int left = frame.x / 2 - 34, top = frame.y / 2 - 51;
            if (row < RestoreRow) return new RectInt(left + 13 + column * (CapWidth + 4), top + row * Pitch, CapWidth, 9);
            return row == RestoreRow ? new RectInt(frame.x / 2 - 6, top + 78, 13, 12) : new RectInt(frame.x / 2 - 13, top + 91, 27, 11);
        }
    }
}
