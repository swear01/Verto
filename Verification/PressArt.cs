using System.IO;
using System.Reflection;
using Phase;
using UnityEngine;

// Renders the store cover and link-preview banner with the game's own pixel renderer (icons: press_icon.py).
// Run through Pipeline run_script, entry PressArt.Run; outputs are nearest-neighbour upscales of native pixels.
public static class PressArt
{
    static readonly string[] title = (string[])typeof(PhaseTitle).GetField("titleText", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

    // dy lifts the whole scene so a shorter view still shows the spike floor.
    static PhaseController Showcase(int dy = 0)
    {
        var blocks = new[] {
            new Block(0, 168 - dy, 96, 16, phaseMask: 7),
            new Block(96, 176 - dy, 168, 8, phaseMask: 7),
            new Block(104, 144 - dy, 40, 8, phaseMask: 1),
            new Block(160, 120 - dy, 40, 8, phaseMask: 2),
            new Block(216, 96 - dy, 40, 8, phaseMask: 4),
            new Block(264, 72 - dy, 56, 112, phaseMask: 7)
        };
        var c = new PhaseController(blocks, 24, 157 - dy,
            spikes: new[] { new Spike(96, 168 - dy, 168, SpikeDirection.Up, 7) },
            lasers: new[] { new Laser(240, 0, LaserDirection.Down, 4) },
            crystals: new[] { new Crystal(176, 80 - dy, CrystalKind.Jump, 2), new Crystal(288, 48 - dy, CrystalKind.Dash, 7) });
        c.phase = 1;
        var p = c.player; p.x = 147; p.y = 104 - dy; p.vy = -60; p.grounded = false; p.facing = 1;
        return c;
    }
    static void Logo(PixelFrame frame, int top, int scale)
    {
        int left = (frame.Width - title[0].Length * scale) / 2;
        frame.Blit(title, left + scale, top + scale, PixelFrame.Ink, scale);
        frame.Blit(title, left, top, PixelFrame.Light, scale);
        int bar = title[0].Length * scale / 3, y = top + title.Length * scale + scale + 1;
        for (int i = 0; i < 3; i++) frame.Fill(left + i * bar, y, bar, scale - 1, PhasePixels.SurfaceColor(new[] { 1, 2, 4 }[i]));
    }
    static void Save(Texture2D source, int k, string path)
    {
        var from = source.GetPixels32(); int w = source.width, h = source.height;
        var to = new Color32[w * k * h * k];
        for (int y = 0; y < h * k; y++) for (int x = 0; x < w * k; x++) to[y * w * k + x] = from[y / k * w + x / k];
        var image = new Texture2D(w * k, h * k, TextureFormat.RGBA32, false);
        image.SetPixels32(to); image.Apply(false);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, image.EncodeToPNG());
        Object.DestroyImmediate(image);
    }
    static void Frame(PhasePixels pixels, PhaseController room, int width, int height, int logoTop, int scale)
    {
        pixels.SetView(width, height, 0);
        pixels.Draw(room);
        var frame = new PixelFrame(pixels.Texture); Logo(frame, logoTop, scale); frame.Apply();
    }
    public static string Run()
    {
        var pixels = new PhasePixels();
        try
        {
            Frame(pixels, Showcase(), 315, 250, 52, 3);
            Save(pixels.Texture, 2, "Documentation/press/cover.png");
            Frame(pixels, Showcase(14), 320, 160, 14, 3);
            Save(pixels.Texture, 4, "Documentation/press/social.png");
            return "cover 630x500, social 1280x640";
        }
        finally { Object.DestroyImmediate(pixels.Texture); }
    }
}
