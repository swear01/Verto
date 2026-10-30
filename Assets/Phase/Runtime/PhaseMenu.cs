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
            int x = Horizontal(k);
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
        // -1, 0 or 1 from this frame's presses: left/right come from their bindings or arrows, up/down from the arrows alone;
        // a tap shorter than a frame still counts.
        public static int Horizontal(Keyboard k) => k == null ? 0 :
            (PhaseSandbox.Pressed(k, 1, true) || k.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (PhaseSandbox.Pressed(k, 0, true) || k.leftArrowKey.wasPressedThisFrame ? 1 : 0);
        public static int Vertical(Keyboard k) => k == null ? 0 : (k.downArrowKey.wasPressedThisFrame ? 1 : 0) - (k.upArrowKey.wasPressedThisFrame ? 1 : 0);
        public static bool Confirmed(Keyboard k) =>
            k != null && (PhaseSandbox.Pressed(k, 2, true) || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
    }

    // Level select: a grid of room thumbnails numbered in pixel digits; every level is always available.
    // Arrows or movement keys move, Enter or jump loads, a click picks, the mouse wheel or a dragged scrollbar scrolls, Escape goes back.
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
        // The column count and zoom that show the whole grid largest within the view; when even scale 1 overflows,
        // as many columns and rows as fit, and the rest scroll.
        static Vector2Int layoutFrame; static int layoutCount = -1; static (int columns, UiZoom zoom, int rows) layout;
        static (int columns, UiZoom zoom, int rows) Layout(Vector2Int frame, int count)
        {
            if (count != layoutCount || frame != layoutFrame) { layout = Measure(frame, count); layoutFrame = frame; layoutCount = count; }
            return layout;
        }
        static (int columns, UiZoom zoom, int rows) Measure(Vector2Int frame, int count)
        {
            (int columns, UiZoom zoom, int rows) best = default; int bestArea = 0;
            foreach (int choice in columnChoices.Concat(Enumerable.Range(1, count)))
            {
                int columns = Mathf.Min(choice, count), rows = (count + columns - 1) / columns;
                int w = columns * (Box + GapX) - GapX, h = rows * (TileHeight + GapY) - GapY;
                var zoom = UiZoom.Fit(frame, w, h);
                int area = UiZoom.Fits(frame, w * zoom.Scale, h * zoom.Scale) ? w * h * zoom.Scale * zoom.Scale : 0;
                if (area > bestArea) { best = (columns, zoom, rows); bestArea = area; }
            }
            if (bestArea > 0) return best;
            int fitColumns = Mathf.Clamp((int)(frame.x * .8f + GapX) / (Box + GapX), 1, count), fitRows = Mathf.Max(1, (int)(frame.y * .8f + GapY) / (TileHeight + GapY));
            return (fitColumns, UiZoom.Fit(frame, fitColumns * (Box + GapX) - GapX, fitRows * (TileHeight + GapY) - GapY), fitRows);
        }
        static (int columns, UiZoom zoom, int rows) Layout(Vector2Int frame) => Layout(frame, Count);
        static RectInt LocalTile(int columns, int i, int firstRow = 0) =>
            new RectInt(i % columns * (Box + GapX), (i / columns - firstRow) * (TileHeight + GapY), Box, TileHeight);
        // Keeps the chosen row inside the visible rows.
        int Scroll(int columns, int rows)
        {
            int row = Choice / columns, total = (Count + columns - 1) / columns;
            return firstRow = Mathf.Clamp(firstRow, Mathf.Max(0, row - rows + 1), Mathf.Min(row, Mathf.Max(0, total - rows)));
        }
        int firstRow, grab;
        bool dragging;
        Vector2? pointer;
        static int BarHeight(int rows) => rows * (TileHeight + GapY) - GapY;
        static int BarX(int columns) => columns * (Box + GapX) - GapX + 3;
        public static int Columns(Vector2Int frame) => Layout(frame).columns;
        public static int Rows(Vector2Int frame) => Layout(frame).rows;
        // A level's tile, thumbnail frame and number, in frame pixels, with the grid scrolled to the top.
        public static RectInt Tile(Vector2Int frame, int i, int count = -1) { var (columns, zoom, _) = Layout(frame, count < 0 ? Count : count); return zoom.Frame(LocalTile(columns, i)); }
        // Changes whenever the drawn grid does, so a caller can skip redrawing an unchanged one.
        public int Signature(Vector2Int frame) => (Choice * 397 ^ firstRow) * 397 ^ frame.x * 8191 ^ frame.y;
        // Returns the chosen level number, 0 when Escape goes back, or -1.
        public int Tick(Keyboard k, Mouse m, Vector2Int frameSize)
        {
            if (k != null && k.escapeKey.wasPressedThisFrame) return 0;
            var (columns, zoom, rows) = Layout(frameSize);
            if (k != null)
            {
                int column = Choice % columns + PhaseMenu.Horizontal(k), row = Choice / columns + PhaseMenu.Vertical(k);
                if (column >= 0 && column < columns && row >= 0 && row * columns + column < Count) Choice = row * columns + column;
            }
            int top = Scroll(columns, rows);
            if (m != null && Screen.width > 0 && Screen.height > 0)
            {
                var at = zoom.Local(PhaseMenu.FramePoint(m, frameSize));
                int total = (Count + columns - 1) / columns, barHeight = BarHeight(rows), barX = BarX(columns), maxTop = Mathf.Max(0, total - rows);
                bool moved = pointer != null && pointer != m.position.ReadValue(), scrolled = false;
                pointer = m.position.ReadValue();
                if (!m.leftButton.isPressed || total <= rows) dragging = false;
                if (total > rows)
                {
                    int thumb = top * barHeight / total, thumbHeight = rows * barHeight / total;
                    if (m.leftButton.wasPressedThisFrame && Mathf.Abs(at.x - barX) <= 3 && at.y >= 0 && at.y < barHeight)
                    { grab = at.y >= thumb && at.y < thumb + thumbHeight ? at.y - thumb : thumbHeight / 2; dragging = true; }
                    int target = top;
                    if (dragging) target = Mathf.RoundToInt((at.y - grab) * total / (float)barHeight);
                    float wheel = m.scroll.ReadValue().y;
                    if (wheel != 0) target = top - (wheel > 0 ? 1 : -1);
                    target = Mathf.Clamp(target, 0, maxTop);
                    if (target != top) { top = firstRow = target; scrolled = true; }
                    if (scrolled) Choice = Mathf.Min(Count - 1, Mathf.Clamp(Choice / columns, top, top + rows - 1) * columns + Choice % columns);
                }
                if (!dragging)
                    for (int i = 0; i < Count; i++)
                        if (i / columns >= top && i / columns < top + rows && LocalTile(columns, i, top).Contains(at))
                        {
                            if (moved) Choice = i;
                            if (m.leftButton.wasPressedThisFrame) { Choice = i; return i + 1; }
                        }
            }
            return PhaseMenu.Confirmed(k) ? Choice + 1 : -1;
        }
        public void Draw(PixelFrame frame)
        {
            frame.Fill(0, 0, frame.Width, frame.Height, PixelFrame.Ink, .85f);
            var (columns, zoom, rows) = Layout(new Vector2Int(frame.Width, frame.Height));
            int top = Scroll(columns, rows);
            var grid = frame.Zoom(zoom);
            thumbnails ??= PhaseSandbox.Levels.Select(Thumbnail).ToArray();
            int totalRows = (Count + columns - 1) / columns;
            if (totalRows > rows)
            {
                int barHeight = BarHeight(rows), x = BarX(columns);
                grid.Fill(x, 0, 1, barHeight, PixelFrame.Light, .2f);
                grid.Fill(x, top * barHeight / totalRows, 1, rows * barHeight / totalRows, PixelFrame.Selection);
            }
            for (int i = 0; i < Count; i++)
            {
                if (i / columns < top || i / columns >= top + rows) continue;
                var tile = LocalTile(columns, i, top); bool selected = i == Choice;
                if (thumbnails[i] != null) grid.Image(thumbnails[i], 40, tile.x + 2, tile.y + 2);
                if (!selected) grid.Fill(tile.x + 2, tile.y + 2, 40, 22, PixelFrame.Ink, .4f);
                grid.Outline(tile.x, tile.y, Box, BoxHeight, selected ? PixelFrame.Selection : PixelFrame.Light, selected ? 1 : .3f);
                if (selected) grid.Outline(tile.x + 1, tile.y + 1, Box - 2, BoxHeight - 2, PixelFrame.Selection);
                string label = (i + 1).ToString();
                grid.Zoom(new UiZoom(2, new Vector2Int(tile.x + Box / 2 - PixelFrame.TextWidth(label), tile.y + BoxHeight + 2))).Text(label, 0, 0, selected ? 1 : .6f);
            }
        }
        // Baked into Resources/Levels by Phase/Editor/LevelThumbnails.cs on every build; a missing one leaves the frame empty.
        static Color32[] Thumbnail(string scene)
        {
            var texture = Resources.Load<Texture2D>("Levels/" + scene);
            return texture != null && texture.isReadable && texture.width == 40 ? texture.GetPixels32() : null;
        }
    }

    // Settings: three action rows (left, right, jump) with two key slots each, effect and music volume sliders,
    // then a row of three controls: restore default keys, fullscreen and close.
    // Arrows move; left and right (or a click) set a slider; Enter, jump or a click captures the next key or uses the control; Backspace clears; Escape cancels or closes.
    public sealed class PhaseKeys
    {
        public const int SfxRow = 3, MusicRow = 4, ControlsRow = 5, RestoreColumn = 0, FullScreenColumn = 1, BackColumn = 2;
        // Corner brackets pointing out to enter fullscreen, and in to leave it.
        static readonly string[] enterFullScreenIcon = { "####...####", "#.........#", "#.........#", "#.........#", "...........", "...........", "...........", "#.........#", "#.........#", "#.........#", "####...####" },
            exitFullScreenIcon = { "...#...#...", "...#...#...", "...#...#...", "####...####", "...........", "...........", "...........", "####...####", "...#...#...", "...#...#...", "...#...#..." },
            // An arrow curling back counter-clockwise, as on every "reset to default" button.
            resetIcon = { "..#........", ".##........", "###.#####..", ".##.....##.", "..#......##", "..........#", "..........#", ".........##", ".......##..", "...#####...", "..........." },
            soundIcon = { "...#...", "..##.#.", "####..#", "####..#", "####..#", "..##.#.", "...#..." },
            musicIcon = { "..#####", "..#...#", "..#...#", "..#...#", "###..##", "###..##", ".#...#." };
        // Unity applies Screen.fullScreen natively and, within the confirming key or click, on Web; the Editor ignores it, so tests swap these.
        public static System.Func<bool> IsFullScreen = () => Screen.fullScreen;
        public static System.Action<bool> SetFullScreen = on => Screen.fullScreen = on;
        static readonly string[] arrowLeft = { "...#...", "..#....", ".#.....", "#######", ".#.....", "..#....", "...#..." };
        static readonly string[][] actionIcons = {
            arrowLeft, arrowLeft.Select(r => new string(r.Reverse().ToArray())).ToArray(),
            new[] { "...#...", "..#.#..", ".#...#.", "...#...", "..#.#..", ".#...#.", "#######" }
        };
        // Bindable key names (Unity Key names, plus Shift for both Shift keys) and their key-cap labels.
        // Up and down arrows stay reserved for menu and editor navigation.
        public static readonly Dictionary<string, string> Labels = new Dictionary<string, string> {
            ["LeftArrow"] = "←", ["RightArrow"] = "→",
            ["Space"] = "SPACE", ["Shift"] = "SHIFT", ["Enter"] = "ENTER", ["Backspace"] = "BKSP",
            ["Comma"] = ",", ["Period"] = ".", ["Slash"] = "/", ["Semicolon"] = ";", ["Quote"] = "'", ["LeftBracket"] = "[",
            ["RightBracket"] = "]", ["Backslash"] = "\\", ["Backquote"] = "`", ["Minus"] = "-", ["Equals"] = "="
        };
        static PhaseKeys()
        {
            for (char c = 'A'; c <= 'Z'; c++) Labels[c.ToString()] = c.ToString();
            for (char c = '0'; c <= '9'; c++) Labels["Digit" + c] = c.ToString();
        }
        const int CapWidth = 25, Pitch = 11, PageWidth = 67, PageHeight = 74, SliderX = 13, SliderWidth = 54;
        public int Row { get; private set; }
        public int Column { get; private set; }
        public bool Capturing { get; private set; }
        public bool Flashing => flash > 0;
        float flash;
        Vector2Int size;

        static UiZoom Page(Vector2Int frame) => UiZoom.Fit(frame, PageWidth, PageHeight);
        // A key cap, a slider, or the restore, fullscreen or close control with its selection border, in page pixels.
        static RectInt Cell(int row, int column) => row switch
        {
            < SfxRow => new RectInt(SliderX + column * (CapWidth + 4), row * Pitch, CapWidth, 9),
            < ControlsRow => new RectInt(SliderX, 35 + (row - SfxRow) * Pitch, SliderWidth, 9),
            _ => new RectInt(column * 26, 59, 15, 15)
        };
        static int Columns(int row) => row < SfxRow ? 2 : row < ControlsRow ? 1 : 3;
        static int Level(int row) => row == SfxRow ? PhaseAudio.SfxLevel : PhaseAudio.MusicLevel;
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
            int vertical = PhaseMenu.Vertical(k), horizontal = PhaseMenu.Horizontal(k);
            if (vertical < 0) Row = Mathf.Max(0, Row - 1);
            if (vertical > 0) Row = Mathf.Min(ControlsRow, Row + 1);
            Column = Mathf.Min(Column, Columns(Row) - 1);
            if (Row is SfxRow or MusicRow) { if (horizontal != 0) PhaseAudio.SetLevel(Row == SfxRow, Level(Row) + horizontal); }
            else Column = Mathf.Clamp(Column + horizontal, 0, Columns(Row) - 1);
            var m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame)
            {
                var at = Page(size).Local(PhaseMenu.FramePoint(m, size));
                for (int row = 0; row <= ControlsRow; row++)
                    for (int column = 0; column < Columns(row); column++)
                    {
                        var cell = Cell(row, column);
                        if (!cell.Contains(at)) continue;
                        Row = row; Column = column;
                        if (row is not (SfxRow or MusicRow)) return Confirm();
                        PhaseAudio.SetLevel(row == SfxRow, Mathf.RoundToInt((at.x - cell.x - 1) * (float)PhaseAudio.MaxLevel / (cell.width - 2)));
                        return false;
                    }
            }
            else if (PhaseMenu.Confirmed(k)) return Confirm();
            else if (k.backspaceKey.wasPressedThisFrame && Row < SfxRow) Set("");
            return false;
        }
        bool Confirm()
        {
            if (Row >= SfxRow && Row < ControlsRow) return false;
            if (Row == ControlsRow)
            {
                if (Column == BackColumn) return true;
                if (Column == FullScreenColumn) SetFullScreen(!IsFullScreen());
                else { PhaseSandbox.ApplyControls(PhaseSandbox.DefaultSlots); PhaseSandbox.SaveControls(); }
            }
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
            for (int row = SfxRow; row < ControlsRow; row++)
            {
                var track = Cell(row, 0); float amount = Row == row ? 1 : .35f;
                page.Blit(row == SfxRow ? soundIcon : musicIcon, 0, track.y + 1, PixelFrame.Light);
                page.Outline(track.x, track.y, track.width, track.height, PixelFrame.Light, amount);
                page.Fill(track.x + 1, track.y + 1, (track.width - 2) * Level(row) / PhaseAudio.MaxLevel, track.height - 2, PixelFrame.Light, amount);
            }
            Control(page, RestoreColumn, resetIcon);
            Control(page, FullScreenColumn, IsFullScreen() ? exitFullScreenIcon : enterFullScreenIcon);
            Control(page, BackColumn, PhaseMenu.CloseIcon);
        }
        void Control(PixelFrame page, int column, string[] icon)
        {
            var box = Cell(ControlsRow, column); bool selected = Row == ControlsRow && Column == column;
            page.Blit(icon, box.x + 2, box.y + 2, PixelFrame.Light, amount: selected ? 1 : .35f);
            if (selected) page.Outline(box.x, box.y, box.width, box.height, PixelFrame.Light);
        }
    }
}
