using UnityEngine;
using UnityEngine.InputSystem;

namespace Phase
{
    public sealed class PhaseTitle : PhaseScreen
    {
        public const string Scene = "PhaseTitle";
        static readonly string[] titleText = {
            "####..#...#..###...####.#####",
            "#...#.#...#.#...#.#.....#....",
            "#...#.#...#.#...#.#.....#....",
            "####..#####.#####..###..####.",
            "#.....#...#.#...#.....#.#....",
            "#.....#...#.#...#.....#.#....",
            "#.....#...#.#...#.####..#####"
        };
        readonly PhaseMenu menu = new PhaseMenu(PhaseMenu.PlayIcon, PhaseMenu.LevelsIcon, PhaseMenu.EditIcon, PhaseMenu.SettingsIcon);
        public int Choice => menu.Choice;
        public PhaseKeys Keys { get; private set; }
        public PhaseLevels LevelSelect { get; private set; }
        PhaseController backdrop;

        // The 44-row banner (title at rows 8–21, icons centered on row 33) at the UI scale that fits the view.
        int Scale => UiZoom.Fit(new Vector2Int(pixels.Width, pixels.Height), 80, 44).Scale;
        int MenuMiddle => pixels.Height / 2 + 11 * Scale;
        protected override void Render()
        {
            backdrop ??= new PhaseController(new[] { new Block(0, 168, 320, 8, phaseMask: 7) }, 24, 157);
            backdrop.ResizeRoom(pixels.Width);
            pixels.Draw(backdrop, false);
            var frame = new PixelFrame(pixels.Texture);
            // Level select and settings replace the title rather than covering it.
            if (Keys != null) Keys.Draw(frame);
            else if (LevelSelect != null) LevelSelect.Draw(frame);
            else
            {
                int scale = Scale, top = frame.Banner(44 * scale);
                frame.Blit(titleText, frame.Width / 2 - titleText[0].Length * scale, top + 8 * scale, PixelFrame.Light, 2 * scale);
                menu.Draw(frame, MenuMiddle, scale);
            }
            frame.Apply();
        }
        protected override void Tick(Keyboard k, Mouse m)
        {
            if (Keys != null)
            {
                if (Keys.Tick(k)) Keys = null;
                Refresh(); return;
            }
            if (LevelSelect != null)
            {
                int level = LevelSelect.Tick(k, m, new Vector2Int(pixels.Width, pixels.Height));
                if (level == 0) LevelSelect = null;
                else if (level > 0) { SelectDemoRoom(level.ToString()); return; }
                Refresh(); return;
            }
            int chosen = menu.Tick(k, m, new Vector2Int(pixels.Width, pixels.Height), MenuMiddle, out bool changed, Scale);
            if (changed) Refresh();
            if (chosen == 0) SelectDemoRoom("1");
            else if (chosen == 1) { LevelSelect = new PhaseLevels(); Refresh(); }
            else if (chosen == 2) Open(PhaseEditor.Scene);
            else if (chosen == 3) { Keys = new PhaseKeys(); Refresh(); }
        }
    }
}
