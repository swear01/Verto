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
        static readonly string[] editIcon = { "....##.", "...#..#", "..#..#.", ".#..#..", "#..#...", "##.#...", "###...." };
        readonly PhaseMenu menu = new PhaseMenu(PhaseMenu.PlayIcon, PhaseMenu.LevelsIcon, editIcon, PhaseMenu.KeysIcon);
        public int Choice => menu.Choice;
        public PhaseKeys Keys { get; private set; }
        public PhaseLevels LevelSelect { get; private set; }
        PhaseController backdrop;

        int MenuTop => pixels.Height / 2 - 22 + 29;
        protected override void Render()
        {
            backdrop ??= new PhaseController(new[] { new Block(0, 168, 320, 8, phaseMask: 7) }, 24, 157);
            backdrop.ResizeRoom(pixels.Width);
            pixels.Draw(backdrop, false);
            var frame = new PixelFrame(pixels.Texture); int top = frame.Banner();
            frame.Blit(titleText, frame.Width / 2 - titleText[0].Length, top + 8, PixelFrame.Light, 2);
            menu.Draw(frame, top + 29);
            Keys?.Draw(frame); LevelSelect?.Draw(frame);
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
            int chosen = menu.Tick(k, m, new Vector2Int(pixels.Width, pixels.Height), MenuTop, out bool changed);
            if (changed) Refresh();
            if (chosen == 0) SelectDemoRoom("1");
            else if (chosen == 1) { LevelSelect = new PhaseLevels(); Refresh(); }
            else if (chosen == 2) Open(PhaseEditor.Scene);
            else if (chosen == 3) { Keys = new PhaseKeys(); Refresh(); }
        }
    }
}
