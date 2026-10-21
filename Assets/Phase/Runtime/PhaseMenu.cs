using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Phase
{
    // A centered row of 11×11 icons: arrows or movement keys choose, Enter or jump confirms, or click an icon.
    public sealed class PhaseMenu
    {
        public static readonly string[] PlayIcon = { "...........", ".##........", ".####......", ".######....", ".########..", ".##########", ".##########", ".########..", ".######....", ".####......", ".##........" },
            SettingsIcon = { "....###....", ".##.###.##.", ".#########.", "..##...##..", "###.....###", "###.....###", "###.....###", "..##...##..", ".#########.", ".##.###.##.", "....###...." },
            LevelsIcon = { "#####.#####", "#####.#####", "##..#.#..##", "#####.#####", "#####.#####", "...........", "#####.#####", "#####.#####", "##..#.#..##", "#####.#####", "#####.#####" },
            EditIcon = { ".......##..", "......####.", ".....#.####", "....###.##.", "...#####...", "..#####....", ".#####.....", ".####......", "###........", "##.........", "#.........." },
            HomeIcon = { "....###....", "...#####...", "..#######..", ".#########.", "###########", "###########", ".###...###.", ".###...###.", ".###...###.", ".###...###.", ".###...###." },
            RetryIcon = { "...####....", ".#######.#.", "###....####", "##.....####", "##.........", "##.......##", "##.......##", "###.....###", ".#########.", "..#######..", "..........." },
            CloseIcon = { "##.......##", "###.....###", ".###...###.", "..###.###..", "...#####...", "....###....", "...#####...", "..###.###..", ".###...###.", "###.....###", "##.......##" };
        public const int Size = 11, Pitch = 22;
        readonly string[][] icons;
        public int Choice { get; private set; }
        // The icon under the mouse, lit like the keyboard choice, or -1.
        public int Pointer { get; private set; } = -1;
        public PhaseMenu(params string[][] icons) { this.icons = icons; }

        int Width => (icons.Length - 1) * Pitch + Size;
        // The largest UI scale at which the row fits the frame.
        public int Fit(Vector2Int frame) => UiZoom.Fit(frame, Width, Size).Scale;
        UiZoom Row(Vector2Int frame, int middle, int scale) => new UiZoom(scale, new Vector2Int((frame.x - Width * scale) / 2, middle - Size * scale / 2));
        // Draws the row centered horizontally, with its vertical center on `middle`.
        public void Draw(PixelFrame frame, int middle, int scale = 1)
        {
            var row = frame.Zoom(Row(new Vector2Int(frame.Width, frame.Height), middle, scale));
            for (int i = 0; i < icons.Length; i++) row.Blit(icons[i], i * Pitch, 0, PixelFrame.Light, amount: i == Choice || i == Pointer ? 1 : .35f);
        }
        // Returns the confirmed icon, or -1; `changed` reports a new selection or pointer highlight.
        public int Tick(Keyboard k, Mouse m, Vector2Int frameSize, int middle, out bool changed, int scale = 1)
        {
            changed = false;
            int x = Step(k, 0, 1);
            if (x != 0 && Choice + x >= 0 && Choice + x < icons.Length) { Choice += x; changed = true; }
            int over = -1;
            if (m != null && Screen.width > 0 && Screen.height > 0)
            {
                var p = Row(frameSize, middle, scale).Local(FramePoint(m, frameSize));
                for (int i = 0; i < icons.Length; i++)
                    if (p.x >= i * Pitch - 3 && p.x < i * Pitch + Size + 3 && p.y >= -3 && p.y < Size + 3) over = i;
            }
            if (over != Pointer) { Pointer = over; changed = true; }
            if (over >= 0 && m.leftButton.wasPressedThisFrame) { Choice = over; return over; }
            return Confirmed(k) ? Choice : -1;
        }
        // The mouse position in frame pixels from the top-left.
        public static Vector2Int FramePoint(Mouse m, Vector2Int frameSize)
        {
            var p = m.position.ReadValue();
            return new Vector2Int(Mathf.FloorToInt(p.x / Screen.width * frameSize.x), Mathf.FloorToInt((1 - p.y / Screen.height) * frameSize.y));
        }
        // -1, 0 or 1 from this frame's presses of two movement actions (0 left, 1 right, 2 up, 3 down) or their arrows;
        // a tap shorter than a frame still counts.
        public static int Step(Keyboard k, int less, int more)
        {
            if (k == null) return 0;
            bool horizontal = less == 0;
            bool down(int action, KeyControl arrow) => PhaseSandbox.Pressed(k, action, true) || arrow.wasPressedThisFrame;
            return (down(more, horizontal ? k.rightArrowKey : k.downArrowKey) ? 1 : 0) - (down(less, horizontal ? k.leftArrowKey : k.upArrowKey) ? 1 : 0);
        }
        public static bool Confirmed(Keyboard k) =>
            k != null && (PhaseSandbox.Pressed(k, 4, true) || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
    }

    // Level select: a grid of room thumbnails numbered in pixel digits; every level is always available.
    // Arrows or movement keys move, Enter or jump loads, a click picks, Escape goes back.
    public sealed class PhaseLevels
    {
        // A tile is a 44×26 frame around the 40×22 thumbnail (one pixel per 8px cell) above its 2× number.
        const int Box = 44, BoxHeight = 26, TileHeight = 38, GapX = 6, GapY = 5;
        static readonly int[] columnChoices = { 4, 5, 6, 3, 2 };
        static Color32[][] thumbnails;
        public int Choice { get; private set; }
        public PhaseLevels(int current = 0) { Choice = Mathf.Clamp(current, 0, Count - 1); }
        static int Count => PhaseSandbox.Levels.Length;
        // The column count and zoom that show the grid largest within the view.
        static (int columns, UiZoom zoom) Layout(Vector2Int frame)
        {
            (int columns, UiZoom zoom) best = default; int bestArea = -1;
            foreach (int columns in columnChoices)
            {
                int w = columns * (Box + GapX) - GapX, h = (Count + columns - 1) / columns * (TileHeight + GapY) - GapY;
                var zoom = UiZoom.Fit(frame, w, h);
                int area = UiZoom.Fits(frame, w * zoom.Scale, h * zoom.Scale) ? w * h * zoom.Scale * zoom.Scale : 0;
                if (area > bestArea) { best = (columns, zoom); bestArea = area; }
            }
            return best;
        }
        static RectInt LocalTile(int columns, int i) => new RectInt(i % columns * (Box + GapX), i / columns * (TileHeight + GapY), Box, TileHeight);
        public static int Columns(Vector2Int frame) => Layout(frame).columns;
        // A level's tile, thumbnail frame and number, in frame pixels.
        public static RectInt Tile(Vector2Int frame, int i) { var (columns, zoom) = Layout(frame); return zoom.Frame(LocalTile(columns, i)); }
        // Returns the chosen level number, 0 when Escape goes back, or -1.
        public int Tick(Keyboard k, Mouse m, Vector2Int frameSize)
        {
            if (k != null && k.escapeKey.wasPressedThisFrame) return 0;
            var (columns, zoom) = Layout(frameSize);
            if (k != null)
            {
                int column = Choice % columns + PhaseMenu.Step(k, 0, 1), row = Choice / columns + PhaseMenu.Step(k, 2, 3);
                if (column >= 0 && column < columns && row >= 0 && row * columns + column < Count) Choice = row * columns + column;
            }
            if (m != null && m.leftButton.wasPressedThisFrame)
            {
                var at = zoom.Local(PhaseMenu.FramePoint(m, frameSize));
                for (int i = 0; i < Count; i++) if (LocalTile(columns, i).Contains(at)) { Choice = i; return i + 1; }
            }
            return PhaseMenu.Confirmed(k) ? Choice + 1 : -1;
        }
        public void Draw(PixelFrame frame)
        {
            frame.Fill(0, 0, frame.Width, frame.Height, PixelFrame.Ink, .85f);
            var (columns, zoom) = Layout(new Vector2Int(frame.Width, frame.Height));
            var grid = frame.Zoom(zoom);
            thumbnails ??= PhaseSandbox.Levels.Select(Thumbnail).ToArray();
            for (int i = 0; i < Count; i++)
            {
                var tile = LocalTile(columns, i); bool selected = i == Choice;
                if (thumbnails[i] != null) grid.Image(thumbnails[i], 40, tile.x + 2, tile.y + 2);
                if (!selected) grid.Fill(tile.x + 2, tile.y + 2, 40, 22, PixelFrame.Ink, .4f);
                grid.Outline(tile.x, tile.y, Box, BoxHeight, selected ? PixelFrame.Selection : PixelFrame.Light, selected ? 1 : .3f);
                if (selected) grid.Outline(tile.x + 1, tile.y + 1, Box - 2, BoxHeight - 2, PixelFrame.Selection);
                string label = (i + 1).ToString();
                grid.Zoom(new UiZoom(2, new Vector2Int(tile.x + Box / 2 - PixelFrame.TextWidth(label), tile.y + BoxHeight + 2))).Text(label, 0, 0, selected ? 1 : .6f);
            }
        }
        // Baked by Verification/LevelThumbnails.cs into Resources/Levels; a missing one leaves the frame empty.
        static Color32[] Thumbnail(string scene)
        {
            var texture = Resources.Load<Texture2D>("Levels/" + scene);
            return texture != null && texture.isReadable && texture.width == 40 ? texture.GetPixels32() : null;
        }
    }

    // Settings: five action rows (left, right, up, down, jump) with two key slots each, restore defaults, the fullscreen switch and close.
    // Arrows move; Enter, jump or a click captures the next key or uses the control; Backspace clears; Escape cancels or closes.
    public sealed class PhaseKeys
    {
        public const int RestoreRow = 5, FullScreenRow = 6, BackRow = 7;
        static readonly string[] fullScreenIcon = { "###.....###", "###.....###", "#.........#", "...........", "...........", "...........", "...........", "...........", "#.........#", "###.....###", "###.....###" };
        // Unity applies Screen.fullScreen natively and, within the confirming key or click, on Web; the Editor ignores it, so tests swap these.
        public static System.Func<bool> IsFullScreen = () => Screen.fullScreen;
        public static System.Action<bool> SetFullScreen = on => Screen.fullScreen = on;
        static readonly string[] arrowLeft = { "...#...", "..#....", ".#.....", "#######", ".#.....", "..#....", "...#..." },
            arrowDown = { "...#...", "...#...", "...#...", "#..#..#", ".#.#.#.", "..###..", "...#..." };
        static readonly string[][] actionIcons = {
            arrowLeft, arrowLeft.Select(r => new string(r.Reverse().ToArray())).ToArray(), arrowDown.Reverse().ToArray(), arrowDown,
            new[] { "...#...", "..#.#..", ".#...#.", "...#...", "..#.#..", ".#...#.", "#######" }
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
        const int CapWidth = 25, Pitch = 11, PageWidth = 67, PageHeight = 106;
        public int Row { get; private set; }
        public int Column { get; private set; }
        public bool Capturing { get; private set; }
        public bool Flashing => flash > 0;
        float flash;
        Vector2Int size;

        static UiZoom Page(Vector2Int frame) => UiZoom.Fit(frame, PageWidth, PageHeight);
        // A key cap, or the restore, fullscreen or close control with its selection border, in page pixels.
        static RectInt Cell(int row, int column) => row switch
        {
            < RestoreRow => new RectInt(13 + column * (CapWidth + 4), row * Pitch, CapWidth, 9),
            RestoreRow => new RectInt(26, 57, 15, 15),
            FullScreenRow => new RectInt(17, 74, 32, 15),
            _ => new RectInt(26, 91, 15, 15)
        };
        // The same in frame pixels.
        public static RectInt Area(Vector2Int frame, int row, int column) => Page(frame).Frame(Cell(row, column));

        // Returns true when Escape or the close control closes the screen.
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
            int vertical = PhaseMenu.Step(k, 2, 3), horizontal = PhaseMenu.Step(k, 0, 1);
            if (vertical < 0) Row = Mathf.Max(0, Row - 1);
            if (vertical > 0) Row = Mathf.Min(BackRow, Row + 1);
            if (horizontal < 0) Column = 0;
            if (horizontal > 0) Column = 1;
            var m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame)
            {
                var at = Page(size).Local(PhaseMenu.FramePoint(m, size));
                for (int row = 0; row <= BackRow; row++)
                    for (int column = 0; column < 2; column++)
                        if (Cell(row, column).Contains(at)) { Row = row; Column = column; return Confirm(); }
            }
            else if (PhaseMenu.Confirmed(k)) return Confirm();
            else if (k.backspaceKey.wasPressedThisFrame && Row < RestoreRow) Set("");
            return false;
        }
        bool Confirm()
        {
            if (Row == BackRow) return true;
            if (Row == FullScreenRow) SetFullScreen(!IsFullScreen());
            else if (Row == RestoreRow) { PhaseSandbox.ApplyControls(PhaseSandbox.DefaultSlots); PhaseSandbox.SaveControls(); }
            else Capturing = true;
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
            size = new Vector2Int(frame.Width, frame.Height);
            frame.Fill(0, 0, frame.Width, frame.Height, PixelFrame.Ink, .85f);
            if (Flashing) frame.Warn();
            var page = frame.Zoom(Page(size));
            var slots = PhaseSandbox.Slots;
            for (int row = 0; row < actionIcons.Length; row++)
            {
                page.Blit(actionIcons[row], 0, row * Pitch + 1, PixelFrame.Light);
                for (int column = 0; column < 2; column++)
                {
                    var cap = Cell(row, column);
                    bool selected = row == Row && column == Column;
                    if (selected && Capturing && Time.unscaledTime * 4 % 2 < 1) page.Fill(cap.x, cap.y, cap.width, cap.height, PixelFrame.Light, .5f);
                    page.Outline(cap.x, cap.y, cap.width, cap.height, PixelFrame.Light, selected ? 1 : .3f);
                    string label = slots[row * 2 + column] == "" ? "" : Labels[slots[row * 2 + column]];
                    page.Text(label, cap.x + (CapWidth - PixelFrame.TextWidth(label) + 1) / 2, cap.y + 2);
                }
            }
            Control(page, RestoreRow, PhaseMenu.RetryIcon);
            // Screen corners, then a switch whose knob sits right on a lit track when fullscreen.
            var full = Control(page, FullScreenRow, fullScreenIcon); bool on = IsFullScreen(); float amount = Row == FullScreenRow ? 1 : .35f;
            page.Outline(full.x + 16, full.y + 4, 13, 7, PixelFrame.Light, amount);
            if (on) page.Fill(full.x + 17, full.y + 5, 11, 5, PixelFrame.Light, amount * .35f);
            page.Fill(full.x + (on ? 23 : 18), full.y + 6, 4, 3, PixelFrame.Light, amount);
            Control(page, BackRow, PhaseMenu.CloseIcon);
        }
        RectInt Control(PixelFrame page, int row, string[] icon)
        {
            var box = Cell(row, 0);
            page.Blit(icon, box.x + 2, box.y + 2, PixelFrame.Light, amount: Row == row ? 1 : .35f);
            if (Row == row) page.Outline(box.x, box.y, box.width, box.height, PixelFrame.Light);
            return box;
        }
    }
}
