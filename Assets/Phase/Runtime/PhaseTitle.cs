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
        internal static readonly string[] PlayIcon = { "#......", "###....", "#####..", "#######", "#####..", "###....", "#......" };
        static readonly string[] editIcon = { "....##.", "...#..#", "..#..#.", ".#..#..", "#..#...", "##.#...", "###...." };
        public int Choice { get; private set; }
        int lastX;
        PhaseController backdrop;

        int IconLeft(int choice) => pixels.Width / 2 + (choice == 0 ? -11 : 4);
        protected override void Render()
        {
            backdrop ??= new PhaseController(new[] { new Block(0, 168, 320, 8, phaseMask: 7) }, 24, 157);
            backdrop.ResizeRoom(pixels.Width);
            pixels.Draw(backdrop, false);
            var frame = new PixelFrame(pixels.Texture); int top = frame.Banner();
            frame.Blit(titleText, frame.Width / 2 - titleText[0].Length, top + 8, PixelFrame.Light, 2);
            frame.Blit(PlayIcon, IconLeft(0), top + 29, PixelFrame.Light, amount: Choice == 0 ? 1 : .35f);
            frame.Blit(editIcon, IconLeft(1), top + 29, PixelFrame.Light, amount: Choice == 1 ? 1 : .35f);
            frame.Apply();
        }
        protected override void Tick(Keyboard k, Mouse m)
        {
            int x = PhaseSandbox.ReadControls(k).x;
            if (x != 0 && x != lastX) { Choice = x < 0 ? 0 : 1; Refresh(); }
            lastX = x;
            if (m != null && m.leftButton.wasPressedThisFrame)
            {
                var p = FramePoint(m); int top = pixels.Height / 2 - 22 + 29;
                for (int choice = 0; choice < 2; choice++)
                    if (p.x >= IconLeft(choice) - 3 && p.x < IconLeft(choice) + 10 && p.y >= top - 3 && p.y < top + 10) { Choice = choice; Confirm(); return; }
            }
            if (Confirmed(k)) Confirm();
        }
        void Confirm()
        {
            if (Choice == 0) SelectDemoRoom("1");
            else Open(PhaseEditor.Scene);
        }
    }
}
