using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Phase
{
    // An integer UI scale and the frame pixel where a layout's (0, 0) lands.
    public readonly struct UiZoom
    {
        public readonly int Scale;
        public readonly Vector2Int Origin;
        public UiZoom(int scale, Vector2Int origin) { Scale = scale; Origin = origin; }
        public static bool Fits(Vector2Int view, int width, int height) => width <= view.x * .8f && height <= view.y * .8f;
        // Centers a width×height layout at the largest integer scale that stays within 80% of the view on both axes.
        public static UiZoom Fit(Vector2Int view, int width, int height)
        {
            int scale = 1;
            while (Fits(view, width * (scale + 1), height * (scale + 1))) scale++;
            return new UiZoom(scale, new Vector2Int((view.x - width * scale) / 2, (view.y - height * scale) / 2));
        }
        public Vector2Int Local(Vector2Int framePoint) =>
            new Vector2Int(Mathf.FloorToInt((framePoint.x - Origin.x) / (float)Scale), Mathf.FloorToInt((framePoint.y - Origin.y) / (float)Scale));
        public RectInt Frame(RectInt local) => new RectInt(Origin + local.position * Scale, local.size * Scale);
    }

    // Draws over a rendered frame; coordinates are frame pixels from the top-left, or layout pixels in a Zoom view.
    public sealed class PixelFrame
    {
        public static readonly Color32 Ink = new Color32(11, 15, 22, 255), Light = new Color32(234, 223, 197, 255), Warning = new Color32(255, 143, 145, 255),
            Selection = new Color32(121, 201, 255, 255);
        readonly Texture2D texture;
        readonly Color32[] pixels;
        readonly int scale = 1, left, top, textureWidth, textureHeight;
        public int Width { get; }
        public int Height { get; }
        public PixelFrame(Texture2D texture)
        { this.texture = texture; pixels = texture.GetPixels32(); Width = textureWidth = texture.width; Height = textureHeight = texture.height; }
        PixelFrame(PixelFrame frame, UiZoom zoom)
        {
            texture = frame.texture; pixels = frame.pixels; textureWidth = frame.textureWidth; textureHeight = frame.textureHeight;
            scale = frame.scale * zoom.Scale; left = frame.left + zoom.Origin.x * frame.scale; top = frame.top + zoom.Origin.y * frame.scale;
            Width = (frame.Width - zoom.Origin.x) / zoom.Scale; Height = (frame.Height - zoom.Origin.y) / zoom.Scale;
        }
        // A view sharing these pixels that draws each pixel as a zoom.Scale square, with (0, 0) at zoom.Origin.
        public PixelFrame Zoom(UiZoom zoom) => new PixelFrame(this, zoom);
        public void Set(int x, int y, Color32 color, float amount = 1)
        {
            for (int py = top + y * scale; py < top + (y + 1) * scale; py++)
                for (int px = left + x * scale; px < left + (x + 1) * scale; px++)
                    if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                    { int i = (textureHeight - 1 - py) * textureWidth + px; pixels[i] = Color32.Lerp(pixels[i], color, amount); }
        }
        public void Fill(int x, int y, int w, int h, Color32 color, float amount = 1)
        { for (int py = y; py < y + h; py++) for (int px = x; px < x + w; px++) Set(px, py, color, amount); }
        public void Outline(int x, int y, int w, int h, Color32 color, float amount = 1)
        {
            Fill(x, y, w, 1, color, amount); Fill(x, y + h - 1, w, 1, color, amount);
            Fill(x, y + 1, 1, h - 2, color, amount); Fill(x + w - 1, y + 1, 1, h - 2, color, amount);
        }
        public void Blit(string[] art, int left, int top, Color32 color, int scale = 1, float amount = 1)
        {
            for (int y = 0; y < art.Length * scale; y++) for (int x = 0; x < art[0].Length * scale; x++)
                if (art[y / scale][x / scale] == '#') Set(left + x, top + y, color, amount);
        }
        // Opaque texture rows as GetPixels32 returns them, bottom row first.
        public void Image(Color32[] image, int width, int left, int top)
        {
            int height = image.Length / width;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) Set(left + x, top + y, image[(height - 1 - y) * width + x]);
        }
        // A red border marks a rejected change.
        public void Warn() { Outline(0, 0, Width, Height, Warning); Outline(1, 1, Width - 2, Height - 2, Warning); }
        // A band across the middle of the frame; returns its top row.
        public int Banner(int rows = 44)
        {
            int top = Height / 2 - rows / 2;
            for (int y = top; y < top + rows; y++)
            {
                bool edge = y == top || y == top + rows - 1;
                Fill(0, y, Width, 1, edge ? Light : Ink, edge ? .35f : .82f);
            }
            return top;
        }
        static readonly Dictionary<char, string[]> font = new Dictionary<string, string> {
            ["A"] = ".#. #.# ### #.# #.#", ["B"] = "##. #.# ##. #.# ##.", ["C"] = ".## #.. #.. #.. .##", ["D"] = "##. #.# #.# #.# ##.",
            ["E"] = "### #.. ##. #.. ###", ["F"] = "### #.. ##. #.. #..", ["G"] = ".## #.. #.# #.# .##", ["H"] = "#.# #.# ### #.# #.#",
            ["I"] = "### .#. .#. .#. ###", ["J"] = "..# ..# ..# #.# .#.", ["K"] = "#.# #.# ##. #.# #.#", ["L"] = "#.. #.. #.. #.. ###",
            ["M"] = "#.# ### ### #.# #.#", ["N"] = "##. #.# #.# #.# #.#", ["O"] = ".#. #.# #.# #.# .#.", ["P"] = "##. #.# ##. #.. #..",
            ["Q"] = ".#. #.# #.# ##. .##", ["R"] = "##. #.# ##. #.# #.#", ["S"] = ".## #.. .#. ..# ##.", ["T"] = "### .#. .#. .#. .#.",
            ["U"] = "#.# #.# #.# #.# ###", ["V"] = "#.# #.# #.# #.# .#.", ["W"] = "#.# #.# ### ### #.#", ["X"] = "#.# #.# .#. #.# #.#",
            ["Y"] = "#.# #.# .#. .#. .#.", ["Z"] = "### ..# .#. #.. ###",
            ["0"] = "### #.# #.# #.# ###", ["1"] = ".#. ##. .#. .#. ###", ["2"] = "##. ..# .#. #.. ###", ["3"] = "##. ..# .#. ..# ##.",
            ["4"] = "#.# #.# ### ..# ..#", ["5"] = "### #.. ##. ..# ##.", ["6"] = ".## #.. ### #.# ###", ["7"] = "### ..# .#. .#. .#.",
            ["8"] = "### #.# ### #.# ###", ["9"] = "### #.# ### ..# ##.",
            [","] = "... ... ... .#. #..", ["."] = "... ... ... ... .#.", ["/"] = "..# ..# .#. #.. #..", [";"] = "... .#. ... .#. #..",
            ["'"] = ".#. .#. ... ... ...", ["["] = "##. #.. #.. #.. ##.", ["]"] = ".## ..# ..# ..# .##", ["\\"] = "#.. #.. .#. ..# ..#",
            ["`"] = "#.. .#. ... ... ...", ["-"] = "... ... ### ... ...", ["="] = "... ### ... ### ...",
            ["←"] = "..#.. .#... ##### .#... ..#..", ["→"] = "..#.. ...#. ##### ...#. ..#..",
            ["↑"] = "..#.. .###. #.#.# ..#.. ..#..", ["↓"] = "..#.. ..#.. #.#.# .###. ..#.."
        }.ToDictionary(g => g.Key[0], g => g.Value.Split(' '));
        public static int TextWidth(string text) => text.Sum(c => font[c][0].Length + 1) - (text.Length > 0 ? 1 : 0);
        // Draws key-cap and level labels in the tiny pixel font.
        public void Text(string text, int left, int top, float amount = 1)
        { foreach (char c in text) { Blit(font[c], left, top, Light, amount: amount); left += font[c][0].Length + 1; } }
        public void Apply() { texture.SetPixels32(pixels); texture.Apply(false); }
    }

    // A non-gameplay scene: owns the pixel surface and answers the same Web bridge messages as PhaseSandbox.
    public abstract class PhaseScreen : MonoBehaviour
    {
        public Camera pixelCamera;
        public Material pixelMaterial;
        protected PhasePixels pixels;
        protected SpriteRenderer surface;
        protected bool leaving;
        Sprite sprite;

        protected virtual void Awake()
        {
            PhaseSandbox.ReleaseInput();
            pixels = new PhasePixels();
            var go = new GameObject("Pixel surface"); go.transform.SetParent(transform);
            surface = go.AddComponent<SpriteRenderer>(); surface.sharedMaterial = pixelMaterial;
            Refresh(true);
        }
        protected void Refresh(bool present = false)
        {
            var size = ViewSize(Screen.width, Screen.height);
            if (present || size.x != pixels.Width || size.y != pixels.Height) { pixels.SetView(size.x, size.y, 0); present = true; }
            Render();
            if (present) sprite = PhaseSandbox.Present(pixels, surface, sprite, pixelCamera);
        }
        // The native view for a screen; the editor widens it to fit its tool panel.
        protected virtual Vector2Int ViewSize(int width, int height) => PhaseSandbox.ViewSize(width, height);
        protected abstract void Render();
        protected abstract void Tick(Keyboard k, Mouse m);
        protected virtual bool Hidden => false;
        void Update()
        {
            if (!Hidden)
            {
                var size = ViewSize(Screen.width, Screen.height);
                if (size.x != pixels.Width || size.y != pixels.Height) Refresh();
            }
            if (leaving || PhaseSandbox.InputPaused(Keyboard.current)) return;
            Tick(Keyboard.current, Mouse.current);
        }
        protected Vector2Int FramePoint(Mouse m) => PhaseMenu.FramePoint(m, new Vector2Int(pixels.Width, pixels.Height));
        protected void Open(string scene) { leaving = true; SceneManager.LoadSceneAsync(scene); }

        public void SetControlsOpen(string value) => PhaseSandbox.Pause(value);
        public void SelectDemoRoom(string value)
        {
            if (leaving) { PhaseSandbox.MapResult(false, PhaseSandbox.Busy); return; }
            try { leaving = true; PhaseSandbox.LoadLevel(value); }
            catch (Exception error) { leaving = false; PhaseSandbox.MapResult(false, error.Message); }
        }
        public virtual void LoadCustomMap(string json)
        {
            if (leaving) { PhaseSandbox.MapResult(false, PhaseSandbox.Busy); return; }
            try
            {
                var map = PhaseCustomMap.Parse(json);
                gameObject.SetActive(false);
                PhaseSandbox.CreateCustom(map, pixelCamera, pixelMaterial);
                PhaseSandbox.Pause("false");
                Destroy(gameObject);
                PhaseSandbox.MapResult(true);
            }
            catch (Exception error)
            {
                if (!gameObject.activeSelf) gameObject.SetActive(true);
                PhaseSandbox.MapResult(false, error.Message);
            }
        }
        protected virtual void OnDestroy() { if (sprite != null) Destroy(sprite); pixels?.Dispose(); }
    }
}
