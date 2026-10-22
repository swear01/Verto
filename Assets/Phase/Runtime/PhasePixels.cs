using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Phase
{
    public sealed class PhasePixels : IDisposable
    {
        public static readonly string[] Highlights = { "ff8f91", "79c9ff", "78d892", "e8e8e8", "f1dc81", "d7a3ed", "85e4dc" };
        static readonly string[] Fills = { "833e54", "356487", "316d4a", "646464", "796d38", "684c7e", "386f78" };
        static readonly string[] Edges = { "b35c70", "4d91b3", "56a271", "a0a0a0", "b3a35b", "a17cb9", "5ba6ac" };
        static readonly int[] surfaceIndices = {3,0,1,5,2,4,6,3};
        public static UnityEngine.Color SurfaceColor(int mask)
        {
            if (mask < 1 || mask > 7) throw new ArgumentOutOfRangeException(nameof(mask));
            return Hex(Highlights[surfaceIndices[mask]]);
        }
        const string Ink = "0b0f16", Glow = "4fe8ff";
        static readonly Dictionary<string, Color32> colors = new Dictionary<string, Color32>();
        Color32[] view = new Color32[320 * 180], buffer;
        public int Width => Texture.width;
        public int Height => Texture.height;
        // View origin in room pixels; Draw clamps it to the room like PhaseCamera.
        public int OriginX { get; private set; }
        public int OriginY { get; private set; }
        // Current drawing target in room pixels: the view, or the room canvas while static art is cached.
        int left, top, cols, rows, roomHeight = 180;
        int BottomY => top + rows;
        (int width, int height, int top, int left, int room) canvas;
        readonly Dictionary<(string, int, int), Bitmap> sprites = new Dictionary<(string, int, int), Bitmap>();
        readonly Dictionary<(int, int), int> cells = new Dictionary<(int, int), int>();
        readonly Dictionary<string, Color32[]> terrain = new Dictionary<string, Color32[]>();
        List<Block> terrainBlocks;
        int terrainBlockCount;
        Color32[] backdrop;
        public Texture2D Texture { get; }
        public PhasePixels()
        { Texture = new Texture2D(320, 180, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PHASE 320x180" }; Target(false); }
        public void SetView(int width, int height, int origin)
        {
            if (width != Width || height != Height)
            {
                if (!Texture.Reinitialize(width, height)) throw new InvalidOperationException("Cannot resize PHASE pixel surface");
                view = new Color32[width * height];
            }
            Scroll(origin, (180 - height) / 2);
        }
        public void Scroll(int x, int y) { OriginX = x; OriginY = y; Target(false); }
        void Target(bool room)
        {
            if (room) { left = canvas.left; top = canvas.top; cols = canvas.width; rows = canvas.height; buffer = new Color32[cols * rows]; }
            else { left = OriginX; top = OriginY; cols = Width; rows = Height; buffer = view; }
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
            x -= left; y -= top;
            if (x < 0 || x >= cols || y < 0 || y >= rows || c.a == 0) return;
            int i = (rows - 1 - y) * cols + x;
            buffer[i] = alpha >= 1 ? c : Mix(buffer[i], c, alpha);
        }
        void Rect(double x, double y, double w, double h, string color, double alpha = 1) => Rect(x, y, w, h, Color(color), alpha);
        void Rect(double x, double y, double w, double h, Color32 color, double alpha = 1)
        {
            int left = Round(x), top = Round(y);
            for (int py = Math.Max(this.top, top); py < Math.Min(BottomY, top + h); py++)
                for (int px = Math.Max(this.left, left); px < Math.Min(this.left + cols, left + w); px++) Put(px, py, color, alpha);
        }
        static uint Hash(int x, int y)
        { unchecked { uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663); h = (h ^ (h >> 13)) * 1274126177; return h ^ (h >> 16); } }
        void Background()
        {
            if (backdrop != null) { Array.Copy(backdrop, buffer, buffer.Length); return; }
            // Neon night: a taller room keeps the skyline at its bottom and adds open sky above; a shorter one sits at the bottom of the view.
            var bands = new[] { "0b0420", "110628", "180932", "200b3c", "2a0d46" }; int g = roomHeight - 180;
            Rect(left, top, cols, Math.Min(0, g) - top, bands[0]);
            Rect(left, 180 + g, cols, BottomY - 180 - g, "07021a");
            Rect(left, 0, cols, g, bands[0]);
            for (int y = 0; y < g; y += 60) for (int x = left / 18 * 18; x < left + cols; x += 18)
                Rect(x, y + 6 + (int)(Hash(x, y + 2) % 50), 1, 1, Hash(x, y + 3) % 3 != 0 ? "3a2466" : "6a3fa0");
            for (int i = 0; i < bands.Length; i++)
            {
                Rect(left, i * 36 + g, cols, 36, bands[i]);
                if (i > 0) for (int y = i * 36 - 2 + g; y < i * 36 + g; y++) for (int x = left / 2 * 2 + (y & 1); x < left + cols; x += 2) Rect(x, y, 1, 1, bands[i]);
            }
            // Striped synth sun sinking behind the skyline.
            for (int y = -22; y <= 22; y++) for (int x = -22; x <= 22; x++)
            {
                int d = x * x + y * y; if (d > 484) continue;
                int row = y + 22; if (y > 0 && (row % 6) < (y / 6 + 1)) continue;
                Rect(232 + x, 92 + g + y, 1, 1, Mix(Color("ffd36e"), Color("ff2f8f"), (y + 22) / 44.0), d > 420 ? .55 : 1);
            }
            var stars = new[] { (18,14),(47,30),(71,9),(103,22),(131,41),(150,12),(178,27),(204,8),(221,44),(240,17),(292,12),(307,46),(86,52),(12,58),(164,60),(283,66) };
            for (int i = 0; i < stars.Length; i++)
            {
                var (x,y) = stars[i]; y += g;
                if (i % 5 == 2) { Rect(x - 1, y, 3, 1, "4a2a7a"); Rect(x, y - 1, 1, 3, "4a2a7a"); Rect(x, y, 1, 1, "c9a4ff"); }
                else Rect(x, y, 1, 1, i % 3 != 0 ? "3a2466" : "7a5ab8");
            }
            for (int x = 320; x < left + cols; x += 18)
                Rect(x, 12 + g + (int)(Hash(x, 0) % 50), 1, 1, "3a2466");
            // Far skyline: dark towers with a cyan rim and scattered lit windows.
            for (int x = left / 6 * 6; x < left + cols; x += 6)
            {
                uint h = Hash(x / 12, 7); int y = 96 + g + (int)(h % 28) - (h % 9 == 0 ? 14 : 0);
                Rect(x, y, 6, 180 + g - y, "14082c"); Rect(x, y, 6, 1, "2fd2ff", .55);
                for (int wy = y + 3; wy < Hill(x) - 2; wy += 4) for (int wx = x + 1; wx < x + 5; wx += 2)
                    if (Hash(wx, wy) % 9 == 0) Rect(wx, wy, 1, 1, Hash(wx, wy + 1) % 2 == 0 ? "ff4fb4" : "4fe8ff", .7);
            }
            // Near skyline: magenta-edged blocks in front of the towers.
            for (int x = left / 4 * 4; x < left + cols; x += 4)
            {
                int y = 2 * Round((118 + 7 * Math.Sin(x * .021) + 5 * Math.Sin(x * .057 + 1.3) + 3 * Math.Sin(x * .13 + .4)) / 2) + g;
                Rect(x, y, 4, 180 + g - y, "0f0524"); Rect(x, y, 4, 1, "ff3ea5", .6);
            }
            // Ground horizon with a receding neon grid.
            for (int x = left / 2 * 2; x < left + cols; x += 2) { int y = Hill(x); Rect(x, y, 2, 180 + g - y, "09021c"); Rect(x, y, 2, 1, "ff3ea5"); }
            int horizon = Hill(160) + 2;
            for (int i = 0, y = horizon; y < 180 + g; i++, y += 2 + i * 2) Rect(left, y, cols, 1, "8a2be2", .5);
            for (int x = left / 16 * 16 - 320; x < left + cols + 320; x += 16)
                for (int y = horizon; y < 180 + g; y++) Rect(x + Round((x - 160) * (y - horizon) / 40.0), y, 1, 1, "8a2be2", .45);
            backdrop = (Color32[])buffer.Clone();
        }
        int Hill(int x) => Round(146 + 5 * Math.Sin(x * .031 + 2) + 3 * Math.Sin(x * .09)) + roomHeight - 180;
        double RenderBottom(Block block) => block.y < roomHeight && block.y + block.h >= roomHeight - 4
            ? Math.Max(block.y + block.h, canvas.top + canvas.height) : block.y + block.h;
        // shortcut: Same-count terrain edits need a revision when live terrain editing is supported.
        // Static backdrop and terrain are cached over the whole room canvas; each frame copies the visible window.
        void Terrain(PhaseController s)
        {
            int width = Math.Max(s.Width, Width), height = Math.Max(s.Height, Height);
            var room = (width, height, s.Height < Height ? s.Height - Height : 0, s.Width < Width ? (s.Width - Width) / 2 : 0, s.Width);
            if (room != canvas) { canvas = room; roomHeight = s.Height; terrainBlocks = null; backdrop = null; terrain.Clear(); }
            OriginX = (int)PhaseCamera.Clamp(OriginX, s.Width, Width, false);
            OriginY = (int)PhaseCamera.Clamp(OriginY, s.Height, Height, true);
            if (terrainBlocks != s.blocks || terrainBlockCount != s.blocks.Count)
            {
                terrainBlocks = s.blocks; terrainBlockCount = s.blocks.Count; cells.Clear(); terrain.Clear();
                foreach (var b in s.blocks) if (!(b is Platform)) for (int y = (int)b.y; y < RenderBottom(b); y += 8) for (int x = (int)b.x; x < b.x + b.w; x += 8) cells[(x,y)] = b.Mask;
            }
            var blocked = new List<int>();
            foreach (int i in s.blocked) if (!(s.blocks[i] is Platform)) blocked.Add(i);
            blocked.Sort();
            string key = s.phase + "|" + string.Join(",", blocked);
            if (!terrain.TryGetValue(key, out var cached))
            {
                Target(true); Background();
                cached = PaintTerrain(s);
                if (terrain.Count > 12) terrain.Clear();
                terrain[key] = cached;
            }
            Target(false);
            for (int y = 0; y < Height; y++)
                Array.Copy(cached, (canvas.height - 1 - (OriginY + y - canvas.top)) * canvas.width + OriginX - canvas.left, view, (Height - 1 - y) * Width, Width);
        }
        Color32[] PaintTerrain(PhaseController s)
        {
            for (int i = 0; i < s.blocks.Count; i++) if (!(s.blocks[i] is Platform)) PaintBlock(s, i);
            return buffer;
        }
        // Platforms move, so they are painted over the cached terrain each frame.
        void Platforms(PhaseController s)
        {
            for (int i = 0; i < s.blocks.Count; i++) if (s.blocks[i] is Platform) PaintBlock(s, i);
        }
        const double SpikeAlpha = .8;
        void PaintBlock(PhaseController s, int i)
        {
            var b = s.blocks[i]; int color = surfaceIndices[b.Mask];
            Color32 cap = Color(Highlights[color]), rim = Color(Edges[color]), body = Color(Fills[color]);
            Color32 deep = Mix(body, Color("000000"), .58);
            bool active = b.ActiveInPhase(s.phase) && !s.blocked.Contains(i), deferred = s.blocked.Contains(i);
            // A moving platform keeps its pattern relative to itself.
            bool isPlatform = b is Platform;
            int ox = isPlatform ? (int)b.x : 0, oy = isPlatform ? (int)b.y : 0;
            for (int Y = (int)b.y; Y < (isPlatform ? b.y + b.h : RenderBottom(b)); Y += 8) for (int X = (int)b.x; X < b.x + b.w; X += 8)
            {
                bool Neighbor(int dx, int dy) => isPlatform ? X + dx * 8 >= b.x && X + dx * 8 < b.x + b.w && Y + dy * 8 >= b.y && Y + dy * 8 < b.y + b.h
                    : cells.TryGetValue((X + dx * 8, Y + dy * 8), out var kind) && kind == b.Mask;
                bool u = Neighbor(0,-1), d = Neighbor(0,1), l = Neighbor(-1,0), r = Neighbor(1,0);
                if (b.spiked && active) { Rect(X, Y, 8, 8, Mix(body, Color("000000"), .5)); Blit(Covered(b.Mask, true), X - 1, Y - 1, SpikeAlpha);
                    continue; }
                if (!active)
                {
                    Rect(X, Y, 8, 8, body, deferred ? .3 : .12);
                    double alpha = deferred ? .95 : .62;
                    for (int n = 0; n < 8; n++)
                    {
                        bool dx = deferred || ((X - ox + n) & 3) < 2 || n == 0 || n == 7, dy = deferred || ((Y - oy + n) & 3) < 2 || n == 0 || n == 7;
                        if (!u && dx) Rect(X + n, Y, 1, 1, cap, alpha);
                        if (!d && dx) Rect(X + n, Y + 7, 1, 1, rim, alpha);
                        if (!l && dy) Rect(X, Y + n, 1, 1, rim, alpha);
                        if (!r && dy) Rect(X + 7, Y + n, 1, 1, rim, alpha);
                    }
                    if (b.spiked) Blit(Covered(b.Mask, false), X - 1, Y - 1, SpikeAlpha);
                    continue;
                }
                // One tile for every colour, white included: outer frame, darker inner frame, darker centre.
                for (int py = 0; py < 8; py++) for (int px = 0; px < 8; px++)
                {
                    int edge = Math.Min(Math.Min(u ? 8 : py, d ? 8 : 7 - py), Math.Min(l ? 8 : px, r ? 8 : 7 - px));
                    Rect(X + px, Y + py, 1, 1, cap, edge < 2 ? .9 : edge < 4 ? .55 : .25);
                }
            }
        }
        static bool Mixed(int mask) => mask == 3 || mask == 5 || mask == 6;
        static readonly string[] spikeRows = {".oo..oo.","oTMooTMo","oLMooLMo","oLMooLMo","LLMMLLMM","LMMDLMMD","MMDDMMDD","DDDDDDDD"};
        static Dictionary<char,Color32> SpikePalette(int mask,bool active)
        {
            int color=surfaceIndices[mask];var cap=Color(Highlights[color]);var rim=Color(Edges[color]);var body=Color(Fills[color]);
            var palette=new Dictionary<char,Color32>{['T']=active?Mix(cap,Color("ffffff"),.6):Mix(rim,Color("101a26"),.45),['L']=active?cap:Mix(body,Color("101a26"),.4),['M']=active?rim:Mix(body,Color("101a26"),.6),['D']=active?body:Mix(body,Color("101a26"),.6)};
            if(active)palette['o']=Color(Ink);
            return palette;
        }
        // A spike-covered cell: teeth on all four sides.
        static readonly string[] coveredRows = {"...TT...","..TLMM..",".oLLMMo.","TLLMMMDD","LLMMMDDD",".oMMDDo.","..MDDD..","...DD..."};
        readonly Dictionary<(int,bool),Bitmap> coveredTiles = new Dictionary<(int,bool),Bitmap>();
        Bitmap Covered(int mask,bool active)
        {
            if(!coveredTiles.TryGetValue((mask,active),out var tile))coveredTiles[(mask,active)]=tile=Paint(coveredRows,SpikePalette(mask,active));
            return tile;
        }
        readonly Dictionary<(int,SpikeDirection,bool),Bitmap> spikeTiles = new Dictionary<(int,SpikeDirection,bool),Bitmap>();
        void Spikes(PhaseController s)
        {
            foreach (var spike in s.spikes)
            {
                bool active=spike.ActiveInPhase(s.phase);var key=(spike.Mask,spike.direction,active);
                if (!spikeTiles.TryGetValue(key,out var tile))
                {
                    var rows=new char[8][];for(int v=0;v<8;v++)rows[v]=new char[8];
                    for(int v=0;v<8;v++)for(int u=0;u<8;u++)
                    {
                        int x=spike.direction==SpikeDirection.Left?v:spike.direction==SpikeDirection.Right?7-v:u;
                        int y=spike.direction==SpikeDirection.Down?7-v:spike.direction==SpikeDirection.Left||spike.direction==SpikeDirection.Right?u:v;
                        rows[y][x]=spikeRows[v][u];
                    }
                    var text=new string[8];for(int v=0;v<8;v++)text[v]=new string(rows[v]);
                    spikeTiles[key]=tile=Paint(text,SpikePalette(spike.Mask,active));
                }
                for(int y=(int)spike.y;y<spike.y+spike.h;y+=8)for(int x=(int)spike.x;x<spike.x+spike.w;x+=8)
                {
                    Blit(tile,x-1,y-1,SpikeAlpha);
                    if(Mixed(spike.Mask)&&(active||(x==spike.x&&y==spike.y)))
                    {
                        int dot=0;for(int phase=0;phase<3;phase++)if(spike.ActiveInPhase(phase))
                        {
                            int at=2+dot++*3;
                            if(spike.direction==SpikeDirection.Up||spike.direction==SpikeDirection.Down)Rect(x+at,y+(spike.direction==SpikeDirection.Up?6:1),2,1,Highlights[phase],active?1:.6);
                            else Rect(x+(spike.direction==SpikeDirection.Left?6:1),y+at,1,2,Highlights[phase],active?1:.6);
                        }
                    }
                }
            }
        }
        void Lasers(PhaseController s)
        {
            foreach (var laser in s.lasers)
            {
                var beam = s.LaserBeam(laser); bool active = laser.ActiveInPhase(s.phase);
                bool horizontal = laser.direction == LaserDirection.Left || laser.direction == LaserDirection.Right;
                int color = surfaceIndices[laser.Mask]; var edge = Color(Highlights[color]);
                var core = Mix(edge, Color("ffffff"), .6);
                if (beam.w > 0 && beam.h > 0)
                {
                    if (active) Rect(beam.x - (horizontal ? 0 : 1), beam.y - (horizontal ? 1 : 0),
                        beam.w + (horizontal ? 0 : 2), beam.h + (horizontal ? 2 : 0), Ink);
                    Rect(beam.x, beam.y, beam.w, beam.h, edge, active ? 1 : .2);
                    Rect(beam.x + (horizontal ? 0 : 1), beam.y + (horizontal ? 1 : 0),
                        beam.w - (horizontal ? 0 : 2), beam.h - (horizontal ? 2 : 0), core, active ? 1 : .28);
                    double endX = laser.direction == LaserDirection.Left ? beam.x : beam.x + beam.w - 1;
                    double endY = laser.direction == LaserDirection.Up ? beam.y : beam.y + beam.h - 1;
                    Rect(horizontal ? endX : beam.x, horizontal ? beam.y : endY,
                        horizontal ? 1 : beam.w, horizontal ? beam.h : 1, core, active ? 1 : .28);
                }
                var rows = new[] { "ss......", "sshhh...", "ssddhl..", "ssdddl..", "ssdddl..", "ssddhl..", "sshhh...", "ss......" };
                for (int v = 0; v < 8; v++) for (int u = 0; u < 8; u++)
                {
                    char ink = rows[v][u]; if (ink == '.') continue;
                    int x = laser.direction == LaserDirection.Left ? 7-u : laser.direction == LaserDirection.Down ? 7-v : laser.direction == LaserDirection.Up ? v : u;
                    int y = laser.direction == LaserDirection.Left ? 7-v : laser.direction == LaserDirection.Down ? u : laser.direction == LaserDirection.Up ? 7-u : v;
                    Rect(laser.x+x, laser.y+y, 1, 1, ink == 's' ? Color("646464") : ink == 'h' ? Color("6a6f8a") : ink == 'l' ? edge : Color("2a2438"));
                }
                // The lens joins the beam at the cell edge in every orientation.
                if (horizontal) Rect(laser.x + (laser.direction == LaserDirection.Right ? 6 : 0), laser.y+2, 2, 4, edge);
                else Rect(laser.x+2, laser.y + (laser.direction == LaserDirection.Down ? 6 : 0), 4, 2, edge);
            }
        }
        static readonly string[] CrystalRows = { "..hhhh..", ".hllleh.", "hlllleeh", "hllbbeeh", "hlbbbeeh", "hebbbeeh", ".heeeeh.", "..hhhh.." };
        void Crystals(PhaseController s)
        {
            foreach (var crystal in s.crystals)
            {
                if (crystal.collected) continue;
                int color = surfaceIndices[crystal.Mask]; var highlight = Color(Highlights[color]);
                var edge = Color(Edges[color]); var body = Color(Fills[color]); var core = Mix(highlight, Color("ffffff"), .6);
                double alpha = crystal.ActiveInPhase(s.phase) ? 1 : .2;
                var rows = CrystalRows;
                bool Solid(int u, int v) => u >= 0 && u < 8 && v >= 0 && v < 8 && rows[v][u] != '.';
                for (int y = -1; y <= 8; y++) for (int x = -1; x <= 8; x++)
                {
                    if (Solid(x,y))
                    {
                        char ink = rows[y][x];
                        Rect(crystal.x+x, crystal.y+y, 1, 1, ink == 'h' ? highlight : ink == 'l' ? core : ink == 'e' ? edge : body, alpha);
                    }
                    else if (Solid(x-1,y) || Solid(x+1,y) || Solid(x,y-1) || Solid(x,y+1))
                        Rect(crystal.x+x, crystal.y+y, 1, 1, Ink, alpha);
                }
                if (crystal.Mask != 7 && (crystal.Mask & (crystal.Mask-1)) != 0)
                {
                    int dot = 0;
                    for (int phase = 0; phase < 3; phase++) if (crystal.ActiveInPhase(phase))
                        Rect(crystal.x+2+dot++*3, crystal.y+5, 1, 1, Highlights[phase], alpha);
                }
            }
        }
        // The goal is a flag, lit in every color.
        static readonly string[] GoalRows = { "sYYYY...", "sYYYYY..", "sYYYY...", "sYY.....", "s.......", "s.......", "s.......", "bbb....." };
        Bitmap goalTile;
        void Goal(PhaseController s)
        {
            if (s.goal == null) return;
            goalTile = goalTile ?? Paint(GoalRows, new Dictionary<char,Color32> { ['s'] = Color("e8e8e8"), ['Y'] = Color(Highlights[4]), ['b'] = Color("a0a0a0") }, outline: true);
            Blit(goalTile, (int)s.goal.x - 1, (int)s.goal.y - 1);
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
                if (c.a == 0 && outline && (At(x-1,y).a != 0 || At(x+1,y).a != 0 || At(x,y-1).a != 0 || At(x,y+1).a != 0)) c = Color(Glow);
                image.pixels[(y + 1) * image.width + x + 1] = c;
            }
            return image;
        }
        void Blit(Bitmap image, int x, int y, double alpha = 1)
        {
            for (int py = 0; py < image.height; py++) for (int px = 0; px < image.width; px++) Put(x + px, y + py, image.pixels[py * image.width + px], alpha);
        }
        // The visor shows phase.
        static readonly Dictionary<string,string[]> Frames = new Dictionary<string,string[]> {
            ["idle"] = new[] { "..cc......","..dcc.....",".dccccc...",".dcccccc..",".dccfffff.",".dcfeefee.",".dcfeefee.","..kkkkk...",".dccccccc.",".dcccccdc.",".ssssssss.","..bb..bb..","..bb..bb.." },
            ["blink"] = new[] { "..cc......","..dcc.....",".dccccc...",".dcccccc..",".dccfffff.",".dcffffff.",".dcfeefee.","..kkkkk...",".dccccccc.",".dcccccdc.",".ssssssss.","..bb..bb..","..bb..bb.." },
            ["run0"] = new[] { "...cc.....","...dcc....","..dccccc..","..dcccccc.","..dccfffff","..dcfeefee","..dcfeefee","...kkkkk..",".dcccccccd","ddccccccc.",".ssssssss.",".bb...BB..","bb.....BB." },
            ["run1"] = new[] { "...dcc....","..dccccc..","..dcccccc.","..dccfffff","..dcfeefee","..dcfeefee","...kkkkk..",".dccccccd.",".dcccccdc.",".ssssssss.",".bbbBB....",".bb.BB....","....BB...." },
            ["run2"] = new[] { "...cc.....","...dcc....","..dccccc..","..dcccccc.","..dccfffff","..dcfeefee","..dcfeefee","...kkkkk..",".dccccccc.","dddcccccc.",".ssssssss.",".BB...bb..","BB.....bb." },
            ["run3"] = new[] { "...dcc....","..dccccc..","..dcccccc.","..dccfffff","..dcfeefee","..dcfeefee","...kkkkk..",".dccccccd.",".dcccccdc.",".ssssssss.",".BBBbb....",".BB.bb....","....bb...." },
            ["jump"] = new[] { "..........","ccc.......",".dcccccc..",".dccccccc.",".dccfffff.",".dcfeefee.",".dcfeefee.","..kkkkk...","ddccccccdd",".dccccccc.",".ssssssss.","..bb.BB...","..b...B..." },
            ["fall"] = new[] { "...cc.....","...cc.....",".dccccc...",".dcccccc..",".dccfffff.",".dcfeefee.",".dcfeefee.","d.kkkkk..d","ddccccccdd",".dccccccc.","ssssssssss","..bb..BB..","..b....B.." },
        };
        static readonly string[] Scarf = { "ff3ea5","b81f78" };
        Bitmap Sprite(string pose, int facing, int phase)
        {
            var key = (pose, facing, phase);
            if (sprites.TryGetValue(key, out var image)) return image;
            var palette = new Dictionary<char,Color32> { ['c']=Color("3b2a78"),['d']=Color("261a52"),['s']=Color("ff3ea5"),['f']=Color("0a0618"),['b']=Color("4a3a92"),['B']=Color("2c2160"),['e']=Color(Highlights[phase]),['k']=Color(Scarf[0]),['h']=Color(Scarf[0]),['H']=Color(Scarf[1]) };
            var rows = (string[])Frames[pose].Clone();
            for (int y = 0; y < rows.Length && !rows[y].Contains("k"); y++) rows[y] = rows[y].Replace('c','h').Replace('d','H');
            return sprites[key] = Paint(rows, palette, facing < 0, true);
        }
        static string Pose(Player p, double t)
        {
            if (!p.grounded) return p.vy < 0 ? "jump" : "fall";
            if (Math.Abs(p.vx) > 7) return "run" + (int)Math.Floor(t * 12) % 4;
            return t % 3.4 < .14 ? "blink" : "idle";
        }
        void DrawPlayer(Player p, double t, int phase)
        {
            string name = Pose(p,t); var rows = Frames[name];
            int x = Round(p.x), feet = Round(p.y + p.h);
            int ky = Array.FindIndex(rows, r => r.Contains("k")), kx = rows[ky].IndexOf('k'), facing = p.facing, length = 5;
            int ax = facing > 0 ? x + kx - 1 : x + 8 - kx, ay = feet - rows.Length + ky;
            bool still = p.grounded && Math.Abs(p.vx) <= 7;
            var segments = new (int x, int y, int h)[length];
            for (int i = 1; i <= length; i++)
            {
                double dy = still ? i * .6 : i * .15 + (p.vy > 0 ? -i * .35 : p.vy < 0 ? i * .25 : 0);
                dy += still ? Math.Sin(t * 3 + i) * .3 : Math.Sin(t * 14 - i * 1.2) * .8 * i / length;
                segments[i - 1] = (ax - facing * i, ay + Round(dy), i <= 2 ? 2 : 1);
            }
            foreach (var q in segments) Rect(q.x, q.y - 1, 1, q.h + 2, Ink);
            var end = segments[length - 1]; Rect(end.x - facing, end.y, 1, end.h, Ink);
            for (int i = 0; i < length; i++) { var q = segments[i]; Rect(q.x, q.y, 1, q.h, Scarf[i == length - 1 ? 1 : 0]); }
            Blit(Sprite(name, facing, phase), x - 2, feet - rows.Length - 1);
            if (p.airJump > 0)
            {
                int cx = x + 3, cy = feet - 5;
                Rect(cx-1, cy-1, 3, 3, Ink);
                Rect(cx, cy-1, 1, 1, "e8e8e8"); Rect(cx-1, cy, 1, 1, "e8e8e8");
                Rect(cx+1, cy, 1, 1, "e8e8e8"); Rect(cx, cy+1, 1, 1, "e8e8e8");
            }
        }
        void VoidBoundary(PhaseController s)
        {
            int floor = s.Height;
            if (BottomY <= floor) return;
            for (int x = left; x < left + cols; x++)
            {
                bool stone = false;
                for (int i = 0; i < s.blocks.Count && !stone; i++)
                {
                    var b = s.blocks[i];
                    stone = !(b is Platform) && b.ActiveInPhase(s.phase) && !s.blocked.Contains(i) &&
                        b.y < floor && b.y + b.h >= floor - 4 && x >= b.x && x < b.x + b.w;
                }
                if (stone) continue;
                for (int y = floor; y < Math.Min(floor + 14, BottomY); y++) Rect(x, y, 1, 1, Ink, (y - floor) / 14.0);
                Rect(x, floor + 14, 1, 16, Ink);
                // Quiet neon terraces step down and darken below the death line; purely visual.
                for (int i = 0, edge = floor + 24; edge < BottomY; i++, edge += 26)
                {
                    double k = Math.Max(.1, .4 - i * .06);
                    int top = edge + (int)(Hash((int)Math.Floor((x - i * 11) / 20.0), i) % 3) * 3;
                    Rect(x, top, 1, edge + 32 - top, Mix(Color(Ink), Color("1a0a3d"), k));
                    Rect(x, top, 1, 1, Mix(Color(Ink), Color("2a1050"), k));
                }
            }
        }
        public void Draw(PhaseController s, bool playerVisible = true)
        {
            Terrain(s);
            Platforms(s);
            Spikes(s);
            Crystals(s);
            Goal(s);
            Lasers(s);
            if (playerVisible && s.respawn <= 0) DrawPlayer(s.player, s.time, s.phase);
            if (playerVisible && s.respawn > 0 && s.respawn < .12)
            {
                int radius = Round(2 + s.respawn * 75);
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4;
                    Rect(s.spawnX + 4 + Round(Math.Cos(angle) * radius), s.spawnY + 5 + Round(Math.Sin(angle) * radius),
                        1, 1, "dce7ee", 1 - s.respawn / .12);
                }
            }
            foreach (var p in s.particles) Rect(p.x, p.y, 1, 1, p.phase < 0 ? "dce7ee" : Highlights[p.phase], Math.Min(1, p.life * 3));
            if (s.flash > 0) Rect(OriginX, OriginY, Width, Height, Highlights[s.phase], s.flash * .3);
            VoidBoundary(s);
            Texture.SetPixels32(view); Texture.Apply(false);
        }
        // The view moves one way: horizontal, or vertical when the rows run bottom-up, so direction 1 moves up.
        public void Slide(Color32[] from, Color32[] to, double progress, int direction, bool vertical = false)
        {
            if (vertical)
            {
                int rise = Round(Height * Math.Max(0, Math.Min(1, progress))) * direction;
                for (int y = 0; y < Height; y++)
                {
                    int row = y + rise; bool old = row >= 0 && row < Height;
                    Array.Copy(old ? from : to, (old ? row : row - direction * Height) * Width, view, y * Width, Width);
                }
                Texture.SetPixels32(view); Texture.Apply(false);
                return;
            }
            int shift = Round(Width * Math.Max(0, Math.Min(1, progress))) * direction;
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            {
                int source = x + shift, row = y * Width;
                view[row + x] = source >= 0 && source < Width ? from[row + source] : to[row + source - direction * Width];
            }
            Texture.SetPixels32(view); Texture.Apply(false);
        }
        public void Dispose() { UnityEngine.Object.Destroy(Texture); }
    }
}
