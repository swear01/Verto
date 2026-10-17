using System;
using System.Collections.Generic;
using UnityEngine;

namespace Phase
{
    public sealed class PhasePixels : IDisposable
    {
        public static readonly string[] Highlights = { "ff8f91", "79c9ff", "78d892", "a9b8c5", "e7ce76" };
        static readonly string[] Fills = { "833e54", "356487", "316d4a", "3c4a57" };
        static readonly string[] Edges = { "b35c70", "4d91b3", "56a271", "697d8e" };
        const string Ink = "0b0f16";
        static readonly Dictionary<string, Color32> colors = new Dictionary<string, Color32>();
        Color32[] buffer = new Color32[320 * 180];
        public int Width => Texture.width;
        public int Height => Texture.height;
        public int OriginY => (180 - Height) / 2;
        int BottomY => OriginY + Height;
        public int OriginX { get; private set; }
        readonly Dictionary<(string, int, int, int, bool), Bitmap> sprites = new Dictionary<(string, int, int, int, bool), Bitmap>();
        readonly Dictionary<(int, int), int> cells = new Dictionary<(int, int), int>();
        readonly Dictionary<string, Color32[]> terrain = new Dictionary<string, Color32[]>();
        List<Block> terrainBlocks;
        int terrainBlockCount;
        Color32[] backdrop;
        public Texture2D Texture { get; }
        public PhasePixels()
        { Texture = new Texture2D(320, 180, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PHASE 320x180" }; }
        public void SetView(int width, int height, int origin)
        {
            if (width == Width && height == Height && origin == OriginX) return;
            if (width != Width || height != Height)
            {
                if (!Texture.Reinitialize(width, height)) throw new InvalidOperationException("Cannot resize PHASE pixel surface");
                buffer = new Color32[width * height];
                terrainBlocks = null;
            }
            OriginX = origin; backdrop = null; terrain.Clear();
        }
        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
        static Color32 Color(string hex)
        {
            if (!colors.TryGetValue(hex, out var c)) colors[hex] = c = Hex(hex);
            return c;
        }
        static int Round(double v) => (int)Math.Floor(v + .5);
        static Color32 Mix(Color32 a, Color32 b, double k) => new Color32((byte)Round(a.r + (b.r - a.r) * k), (byte)Round(a.g + (b.g - a.g) * k), (byte)Round(a.b + (b.b - a.b) * k), 255);
        void Put(int x, int y, Color32 c, double alpha = 1)
        {
            x -= OriginX; y -= OriginY;
            if (x < 0 || x >= Width || y < 0 || y >= Height || c.a == 0) return;
            int i = (Height - 1 - y) * Width + x;
            buffer[i] = alpha >= 1 ? c : Mix(buffer[i], c, alpha);
        }
        void Rect(double x, double y, double w, double h, string color, double alpha = 1) => Rect(x, y, w, h, Color(color), alpha);
        void Rect(double x, double y, double w, double h, Color32 color, double alpha = 1)
        {
            int left = Round(x), top = Round(y);
            for (int py = Math.Max(OriginY, top); py < Math.Min(BottomY, top + h); py++)
                for (int px = Math.Max(OriginX, left); px < Math.Min(OriginX + Width, left + w); px++) Put(px, py, color, alpha);
        }
        static uint Hash(int x, int y)
        { unchecked { uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663); h = (h ^ (h >> 13)) * 1274126177; return h ^ (h >> 16); } }
        void Background()
        {
            if (backdrop != null) { Array.Copy(backdrop, buffer, buffer.Length); return; }
            var bands = new[] { "0a1018", "0c131d", "0e1520", "101a26", "121e2b" };
            Rect(OriginX, OriginY, Width, -OriginY, bands[0]);
            Rect(OriginX, 180, Width, BottomY - 180, "0b111a");
            for (int y = (int)Math.Floor(OriginY / 18.0) * 18; y < 0; y += 18) for (int x = OriginX / 24 * 24; x < OriginX + Width; x += 24)
                if (Hash(x, y) % 4 == 0) Rect(x + Hash(x, y) % 17, y + Hash(y, x) % 13, 1, 1, "2a394c");
            for (int i = 0; i < bands.Length; i++)
            {
                Rect(OriginX, i * 36, Width, 36, bands[i]);
                if (i > 0) for (int y = i * 36 - 2; y < i * 36; y++) for (int x = OriginX / 2 * 2 + (y & 1); x < OriginX + Width; x += 2) Rect(x, y, 1, 1, bands[i]);
            }
            for (int y = -11; y <= 11; y++) for (int x = -11; x <= 11; x++)
            {
                if (x * x + y * y > 110) continue;
                Rect(262 + x, 34 + y, 1, 1, (x + 4) * (x + 4) + (y + 3) * (y + 3) <= 86 ? "162131" : x * x + y * y > 86 ? "24344a" : "1d2b3d");
            }
            var stars = new[] { (18,14),(47,30),(71,9),(103,22),(131,41),(150,12),(178,27),(204,8),(221,44),(240,17),(292,12),(307,46),(86,52),(12,58),(164,60),(283,66) };
            for (int i = 0; i < stars.Length; i++)
            {
                var (x,y) = stars[i];
                if (i % 5 == 2) { Rect(x - 1, y, 3, 1, "26364a"); Rect(x, y - 1, 1, 3, "26364a"); Rect(x, y, 1, 1, "4a5d74"); }
                else Rect(x, y, 1, 1, i % 3 != 0 ? "2a394c" : "3c4e64");
            }
            for (int x = 320; x < OriginX + Width; x += 18)
                Rect(x, 12 + (int)(Hash(x, 0) % 50), 1, 1, "2a394c");
            for (int x = OriginX / 4 * 4; x < OriginX + Width; x += 4)
            {
                int y = 2 * Round((104 + 9 * Math.Sin(x * .021) + 6 * Math.Sin(x * .057 + 1.3) + 3 * Math.Sin(x * .13 + .4)) / 2);
                Rect(x, y, 4, 180 - y, "152131"); Rect(x, y, 4, 1, "1b293b");
            }
            foreach (var pillar in new[] { (26,6,30,1),(44,6,30,1),(96,5,18,0),(150,6,40,1),(204,5,14,0),(246,6,26,1),(264,6,26,1),(300,5,20,0) })
            {
                var (x,w,h,cap) = pillar; int top = Hill(x + 3) - h;
                Rect(x, top, w, h + 4, "0f1722");
                if (cap != 0) { Rect(x - 1, top, w + 2, 2, "0f1722"); Rect(x - 1, top, w + 2, 1, "162030"); }
                else { Rect(x + 1, top - 2, 2, 2, "0f1722"); Rect(x, top, 1, 1, "162030"); }
            }
            Rect(26, Hill(29) - 30, 24, 3, "0f1722"); Rect(26, Hill(29) - 30, 24, 1, "162030");
            Rect(246, Hill(249) - 26, 26, 3, "0f1722"); Rect(246, Hill(249) - 26, 26, 1, "162030");
            for (int x = 340; x < OriginX + Width; x += 48)
            {
                int top = Hill(x + 3) - 18 - (int)(Hash(x, 1) % 20);
                Rect(x, top, 6, 160 - top, "0f1722"); Rect(x - 1, top, 8, 1, "162030");
            }
            for (int x = OriginX / 2 * 2; x < OriginX + Width; x += 2) { int y = Hill(x); Rect(x, y, 2, 180 - y, "0b111a"); Rect(x, y, 2, 1, "111a26"); }
            backdrop = (Color32[])buffer.Clone();
        }
        static int Hill(int x) => Round(146 + 5 * Math.Sin(x * .031 + 2) + 3 * Math.Sin(x * .09));
        double RenderBottom(Block block) => block.y < 180 && block.y + block.h >= 180
            ? Math.Max(block.y + block.h, BottomY) : block.y + block.h;
        // shortcut: Same-count terrain edits need a revision when live terrain editing is supported.
        void Terrain(PhaseController s)
        {
            if (terrainBlocks != s.blocks || terrainBlockCount != s.blocks.Count)
            {
                terrainBlocks = s.blocks; terrainBlockCount = s.blocks.Count; cells.Clear(); terrain.Clear();
                foreach (var b in s.blocks) for (int y = (int)b.y; y < RenderBottom(b); y += 8) for (int x = (int)b.x; x < b.x + b.w; x += 8) cells[(x,y)] = b.phase;
            }
            var blocked = new List<int>(s.blocked); blocked.Sort();
            string key = s.phase + "|" + string.Join(",", blocked);
            if (terrain.TryGetValue(key, out var cached)) { Array.Copy(cached, buffer, buffer.Length); return; }
            Background();
            for (int i = 0; i < s.blocks.Count; i++)
            {
                var b = s.blocks[i]; int color = b.phase < 0 ? 3 : b.phase;
                Color32 cap = Color(Highlights[color]), rim = Color(Edges[color]), body = Color(Fills[color]);
                Color32 lift = Mix(body, rim, .45), dim = Mix(body, Color("000000"), .14), seam = Mix(body, Color("000000"), .4), deep = Mix(body, Color("000000"), .58);
                bool active = (b.phase < 0 || b.phase == s.phase) && !s.blocked.Contains(i), deferred = s.blocked.Contains(i);
                for (int Y = (int)b.y; Y < RenderBottom(b); Y += 8) for (int X = (int)b.x; X < b.x + b.w; X += 8)
                {
                    bool Neighbor(int dx, int dy) => cells.TryGetValue((X + dx * 8, Y + dy * 8), out var kind) && kind == b.phase;
                    bool u = Neighbor(0,-1), d = Neighbor(0,1), l = Neighbor(-1,0), r = Neighbor(1,0);
                    if (!active)
                    {
                        Rect(X, Y, 8, 8, body, deferred ? .3 : .12);
                        double alpha = deferred ? .95 : .62;
                        for (int n = 0; n < 8; n++)
                        {
                            bool dx = deferred || ((X + n) & 3) < 2 || n == 0 || n == 7, dy = deferred || ((Y + n) & 3) < 2 || n == 0 || n == 7;
                            if (!u && dx) Rect(X + n, Y, 1, 1, cap, alpha);
                            if (!d && dx) Rect(X + n, Y + 7, 1, 1, rim, alpha);
                            if (!l && dy) Rect(X, Y + n, 1, 1, rim, alpha);
                            if (!r && dy) Rect(X + 7, Y + n, 1, 1, rim, alpha);
                        }
                        continue;
                    }
                    for (int y = 0; y < 8; y++)
                    {
                        int course = (Y + y) >> 2, row = (Y + y) & 3;
                        if (row == 3) { Rect(X, Y + y, 8, 1, seam); continue; }
                        for (int x = 0; x < 8; x++)
                        {
                            int wx = X + x + (course & 1) * 4, brick = wx >> 3, along = wx & 7;
                            bool joint = along == 7 && Hash(brick, course * 7 + 3) % 3 != 0;
                            Rect(X + x, Y + y, 1, 1, joint ? seam : row == 0 && along < 2 ? lift : Hash(brick, course) % 5 == 0 ? dim : body);
                        }
                    }
                    int top = u ? 0 : 2;
                    if (!u) { Rect(X, Y, 8, 1, cap); Rect(X, Y + 1, 8, 1, rim); }
                    if (!d) Rect(X, Y + 7, 8, 1, deep);
                    if (!l) { Rect(X, Y + top, 1, 8 - top, rim); if (!d) Rect(X, Y + 7, 1, 1, seam); }
                    if (!r) { Rect(X + 7, Y + top, 1, 8 - top, seam); if (!d) Rect(X + 7, Y + 7, 1, 1, deep); }
                    if (!u && !l) Rect(X, Y, 1, 1, rim);
                    if (!u && !r) { Rect(X + 7, Y, 1, 1, rim); Rect(X + 7, Y + 1, 1, 1, lift); }
                    if (u && l && !Neighbor(-1,-1)) Rect(X, Y, 1, 1, seam);
                    if (u && r && !Neighbor(1,-1)) Rect(X + 7, Y, 1, 1, seam);
                    if (u && d && l && r && Hash(X,Y) % 11 == 0) { Rect(X + 2, Y + 1, 1, 1, seam); Rect(X + 3, Y + 2, 1, 1, seam); Rect(X + 3, Y + 4, 1, 1, deep); }
                }
            }
            if (terrain.Count > 12) terrain.Clear();
            terrain[key] = (Color32[])buffer.Clone();
        }
        sealed class Bitmap { public int width, height; public Color32[] pixels; }
        static Bitmap Paint(string[] rows, Dictionary<char,Color32> palette, bool flip = false, bool outline = false)
        {
            int w = rows[0].Length, h = rows.Length;
            var image = new Bitmap { width = w + 2, height = h + 1, pixels = new Color32[(w + 2) * (h + 1)] };
            Color32 At(int x, int y) => x >= 0 && x < w && y >= 0 && y < h && palette.TryGetValue(rows[y][flip ? w - 1 - x : x], out var c) ? c : default;
            for (int y = -1; y < h; y++) for (int x = -1; x <= w; x++)
            {
                Color32 c = At(x,y);
                if (c.a == 0 && outline && (At(x-1,y).a != 0 || At(x+1,y).a != 0 || At(x,y-1).a != 0 || At(x,y+1).a != 0)) c = Color(Ink);
                image.pixels[(y + 1) * image.width + x + 1] = c;
            }
            return image;
        }
        void Blit(Bitmap image, int x, int y, double alpha = 1)
        {
            for (int py = 0; py < image.height; py++) for (int px = 0; px < image.width; px++) Put(x + px, y + py, image.pixels[py * image.width + px], alpha);
        }
        static readonly Bitmap moverTile = Paint(new[] { "TTTTTTTT","hRbbbbRh","hbbbbbbs","hbhhhhbs","hbhsshbs","hbbbbbbs","hRbbbbRs","ssssssss" }, new Dictionary<char, Color32> { ['T']=Color("f2dc8a"),['R']=Color("e7ce76"),['b']=Color("6f603c"),['h']=Color("8f7c47"),['s']=Color("4b4129") });
        void Mover(MovingPlatform m)
        {
            for (int i = 0; i < (m.returns ? 1 : m.nodes.Length); i++)
            {
                var a = m.nodes[i]; var b = m.nodes[(i + 1) % m.nodes.Length];
                double length = Math.Max(Math.Abs(b[0] - a[0]), Math.Abs(b[1] - a[1]));
                for (int n = 0; n < length; n++) if (n % 4 < 2)
                {
                    double x = a[0] + m.w / 2 + (b[0] - a[0]) * n / length, y = a[1] + m.h / 2 + (b[1] - a[1]) * n / length;
                    if (a[1] == b[1]) Rect(x, y - 1, 1, 2, "e0c569", .25);
                    else if (a[0] == b[0]) Rect(x - 1, y, 2, 1, "e0c569", .25);
                    else Rect(x, y, 1, 1, "e0c569", .5);
                }
            }
            foreach (var n in m.nodes) Rect(n[0] + m.w / 2 - 1, n[1] + 2, 3, 3, "e0c569", .5);
            Rect(m.x - 1, m.y - 1, m.w + 2, m.h + 2, Ink);
            for (double y = m.y; y < m.y + m.h; y += 8) for (double x = m.x; x < m.x + m.w; x += 8) Blit(moverTile, Round(x) - 1, Round(y) - 1);
        }
        // Original Wick frames; the hood now shares the scarf's dash-charge colors.
        static readonly Dictionary<string,string[]> Frames = new Dictionary<string,string[]> {
            ["idle"] = new[] { "..cc......","..dcc.....",".dccccc...",".dcccccc..",".dccfffff.",".dcffefef.",".dccffff..","..kkkkk...",".dccccccc.",".dcccccdc.",".ssssssss.","..bb..bb..","..bb..bb.." },
            ["blink"] = new[] { "..cc......","..dcc.....",".dccccc...",".dcccccc..",".dccfffff.",".dcffffff.",".dccfefe..","..kkkkk...",".dccccccc.",".dcccccdc.",".ssssssss.","..bb..bb..","..bb..bb.." },
            ["run0"] = new[] { "...cc.....","...dcc....","..dccccc..","..dcccccc.","..dccfffff","..dcffefef","..dccffff.","...kkkkk..",".dcccccccd","ddccccccc.",".ssssssss.",".bb...BB..","bb.....BB." },
            ["run1"] = new[] { "...dcc....","..dccccc..","..dcccccc.","..dccfffff","..dcffefef","..dccffff.","...kkkkk..",".dccccccd.",".dcccccdc.",".ssssssss.",".bbbBB....",".bb.BB....","....BB...." },
            ["run2"] = new[] { "...cc.....","...dcc....","..dccccc..","..dcccccc.","..dccfffff","..dcffefef","..dccffff.","...kkkkk..",".dccccccc.","dddcccccc.",".ssssssss.",".BB...bb..","BB.....bb." },
            ["run3"] = new[] { "...dcc....","..dccccc..","..dcccccc.","..dccfffff","..dcffefef","..dccffff.","...kkkkk..",".dccccccd.",".dcccccdc.",".ssssssss.",".BBBbb....",".BB.bb....","....bb...." },
            ["jump"] = new[] { "..........","ccc.......",".dcccccc..",".dccccccc.",".dccfffff.",".dcffefef.",".dccffff..","..kkkkk...","ddccccccdd",".dccccccc.",".ssssssss.","..bb.BB...","..b...B..." },
            ["fall"] = new[] { "...cc.....","...cc.....",".dccccc...",".dcccccc..",".dccfffff.",".dcffefef.",".dccffff..","d.kkkkk..d","ddccccccdd",".dccccccc.","ssssssssss","..bb..BB..","..b....B.." },
            ["dash"] = new[] { "..........","ccc.......",".ddccccc..","..dcccccc.","..dccfffff","..dcffefef","..dccffff.","...kkkkk..",".dcccccccd","ddccccccc.","ssssss....","bbb...BB..","......BB.." },
            ["climb0"] = new[] { "..cc....d.","..dcc...d.",".dccccc.d.",".dccccccd.",".dcccfffd.",".dccffefd.",".dcccfff..","..kkkkkk..",".dccccccd.",".dcccccdd.",".ssssss...","..bb.bbb..","..bb......" },
            ["climb1"] = new[] { "..cc......","..dcc.....",".dccccc...",".dccccccd.",".dcccfffd.",".dccffefd.",".dcccfffd.","..kkkkkkd.",".dccccccd.",".dcccccc..",".ssssss...","..BBB.bb..","......bb.." },
            ["duck"] = new[] { "..........","..cc......",".dcccccc..",".dccfffff.",".dcffefef.","..kkkkkk..","sdcccccccs",".bbssssbb." },
        };
        static readonly string[][] Scarf = { new[] { "b96532","763d24" }, new[] { "ffb24d","d5732b" } };
        Bitmap Sprite(string pose, int facing, int phase, int dash, bool silhouette = false)
        {
            var key = (pose, facing, phase, dash, silhouette);
            if (sprites.TryGetValue(key, out var image)) return image;
            var palette = new Dictionary<char,Color32> { ['c']=Color("eadfc5"),['d']=Color("b3a386"),['s']=Color("7d6e5c"),['f']=Color("1a1f2c"),['b']=Color("453c59"),['B']=Color("2c263b"),['e']=Color(Highlights[phase]),['k']=Color(Scarf[dash][0]),['h']=Color(Scarf[dash][0]),['H']=Color(Scarf[dash][1]) };
            var rows = (string[])Frames[pose].Clone();
            for (int y = 0; y < rows.Length && !rows[y].Contains("k"); y++) rows[y] = rows[y].Replace('c','h').Replace('d','H');
            if (silhouette) foreach (char k in "cdsfbBekhH") palette[k] = Color(Highlights[phase]);
            return sprites[key] = Paint(rows, palette, facing < 0, !silhouette);
        }
        static string Pose(Player p, double t)
        {
            if (p.climbing) return p.climbMove != 0 && (int)Math.Floor(t * (p.climbMove < 0 ? 8 : 11)) % 2 != 0 ? "climb1" : "climb0";
            if (p.dashTime > 0) return p.dx != 0 ? "dash" : p.dy < 0 ? "jump" : "fall";
            if (p.ducking) return "duck";
            if (!p.grounded) return p.vy < 0 ? "jump" : "fall";
            if (Math.Abs(p.vx) > 7) return "run" + (int)Math.Floor(t * 12) % 4;
            return t % 3.4 < .14 ? "blink" : "idle";
        }
        void DrawPlayer(Player p, double t, int phase)
        {
            string name = Pose(p,t); var rows = Frames[name];
            int x = Round(p.x), feet = Round(p.y + p.h) + (p.climbing && p.stamina <= 20 && (int)Math.Floor(t * 8) % 2 != 0 ? 1 : 0);
            int ky = Array.FindIndex(rows, r => r.Contains("k")), kx = rows[ky].IndexOf('k'), facing = p.facing, dash = p.dash != 0 ? 1 : 0, length = dash != 0 ? 5 : 3;
            int ax = facing > 0 ? x + kx - 1 : x + 8 - kx, ay = feet - rows.Length + ky;
            bool still = p.climbing || name == "duck" || (p.grounded && Math.Abs(p.vx) <= 7);
            var segments = new (int x, int y, int h)[length];
            for (int i = 1; i <= length; i++)
            {
                double dy = name == "dash" ? 0 : still ? i * (dash != 0 ? .6 : 1) : i * (dash != 0 ? .15 : .7) + (p.vy > 0 ? -i * .35 : p.vy < 0 ? i * .25 : 0);
                if (name != "dash") dy += still ? Math.Sin(t * 3 + i) * .3 : Math.Sin(t * 14 - i * 1.2) * .8 * i / length;
                segments[i - 1] = (p.climbing ? ax - facing * (int)Math.Ceiling(i / 2.0) : ax - facing * i, ay + Round(dy), i <= 2 ? 2 : 1);
            }
            foreach (var q in segments) Rect(q.x, q.y - 1, 1, q.h + 2, Ink);
            var end = segments[length - 1]; Rect(end.x - facing, end.y, 1, end.h, Ink);
            for (int i = 0; i < length; i++) { var q = segments[i]; Rect(q.x, q.y, 1, q.h, Scarf[dash][i == length - 1 ? 1 : 0]); }
            Blit(Sprite(name, facing, phase, dash), x - 2, feet - rows.Length - 1);
        }
        public void Draw(PhaseController s, bool playerVisible = true)
        {
            Terrain(s);
            if (s.mover != null) Mover(s.mover);
            foreach (var t in s.trails) Blit(Sprite("dash", s.player.facing, t.phase, 0, true), Round(t.x) - 2, Round(t.y) - 3, t.life * 2);
            if (playerVisible && s.respawn <= 0) DrawPlayer(s.player, s.time, s.phase);
            foreach (var p in s.particles) Rect(p.x, p.y, 1, 1, p.phase < 0 ? "dce7ee" : Highlights[p.phase], Math.Min(1, p.life * 3));
            if (s.flash > 0) Rect(OriginX, OriginY, Width, Height, Highlights[s.phase], s.flash * .3);
            Texture.SetPixels32(buffer); Texture.Apply(false);
        }
        public void Slide(Color32[] from, Color32[] to, double progress, int direction)
        {
            int shift = Round(Width * Math.Max(0, Math.Min(1, progress))) * direction;
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            {
                int source = x + shift, row = y * Width;
                buffer[row + x] = source >= 0 && source < Width ? from[row + source] : to[row + source - direction * Width];
            }
            Texture.SetPixels32(buffer); Texture.Apply(false);
        }
        public void Dispose() { UnityEngine.Object.Destroy(Texture); }
    }
}
