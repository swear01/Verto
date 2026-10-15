using System;
using UnityEngine;

namespace Phase
{
    public sealed class PhasePixels : IDisposable
    {
        public static readonly string[] Highlights = { "ff8f91", "79c9ff", "78d892", "a9b8c5", "e7ce76" };
        static readonly string[] Fills = { "833e54", "356487", "316d4a", "3c4a57", "6f603c" };
        static readonly string[] Edges = { "b35c70", "4d91b3", "56a271", "697d8e", "b09a53" };
        readonly Color32[] buffer = new Color32[320 * 180];
        public Texture2D Texture { get; }
        public PhasePixels()
        { Texture = new Texture2D(320, 180, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PHASE 320x180" }; }
        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
        void Rect(double x, double y, double w, double h, string color, double alpha = 1)
        {
            Color32 c = Hex(color); int left = (int)Math.Floor(x + .5), top = (int)Math.Floor(y + .5);
            for (int py = Math.Max(0, top); py < Math.Min(180, top + h); py++)
                for (int px = Math.Max(0, left); px < Math.Min(320, left + w); px++)
                {
                    int index = (179 - py) * 320 + px;
                    buffer[index] = alpha >= 1 ? c : (Color32)Color.Lerp(buffer[index], c, (float)alpha);
                }
        }
        void Polygon(string color, params Vector2Int[] points)
        {
            for (int y = 0; y < 180; y++) for (int x = 0; x < 320; x++)
            {
                bool inside = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                    if ((points[i].y > y) != (points[j].y > y) && x < (points[j].x - points[i].x) * (y - points[i].y) / (double)(points[j].y - points[i].y) + points[i].x) inside = !inside;
                if (inside) buffer[(179 - y) * 320 + x] = Hex(color);
            }
        }
        // Cache the static backdrop; animated foreground stays on the source pixel grid.
        readonly Color32[][] backdrops = new Color32[2][];
        void Background(int phase, double time)
        {
            int palette = phase == 0 ? 0 : 1;
            if (backdrops[palette] != null) Array.Copy(backdrops[palette], buffer, buffer.Length);
            else
            {
            for (int y = 0; y < 180; y++)
            {
                Color top = Hex("101a2b"), middle = Hex(palette == 0 ? "1b3047" : "29283c"), bottom = Hex("131e2c");
                Color32 row = y < 117 ? Color.Lerp(top, middle, y / 117f) : Color.Lerp(middle, bottom, (y - 117) / 63f);
                for (int x = 0; x < 320; x++) buffer[(179 - y) * 320 + x] = row;
            }
            for (int i = 0; i < 65; i++) Rect((i * 137 + 37) % 320, (i * i * 19) % 103, 1, 1, "c0d4e8", .4);
            Rect(255, 28, 10, 14, "c2cfbd"); Rect(252, 26, 10, 12, "142034");
            Polygon("1c2b3f", new Vector2Int(0,120),new Vector2Int(27,90),new Vector2Int(49,105),new Vector2Int(90,52),new Vector2Int(127,98),new Vector2Int(143,79),new Vector2Int(183,118),new Vector2Int(210,76),new Vector2Int(250,107),new Vector2Int(289,70),new Vector2Int(320,103),new Vector2Int(320,180),new Vector2Int(0,180));
            Polygon("314254",new Vector2Int(63,83),new Vector2Int(90,52),new Vector2Int(117,84),new Vector2Int(99,73),new Vector2Int(91,77),new Vector2Int(83,69),new Vector2Int(77,78));
            Polygon("22394c",new Vector2Int(0,140),new Vector2Int(23,117),new Vector2Int(55,137),new Vector2Int(80,110),new Vector2Int(103,130),new Vector2Int(141,94),new Vector2Int(183,141),new Vector2Int(214,118),new Vector2Int(252,137),new Vector2Int(287,110),new Vector2Int(320,133),new Vector2Int(320,180),new Vector2Int(0,180));
            Polygon("182c3c",new Vector2Int(0,150),new Vector2Int(47,133),new Vector2Int(83,151),new Vector2Int(123,129),new Vector2Int(164,154),new Vector2Int(208,140),new Vector2Int(250,154),new Vector2Int(297,134),new Vector2Int(320,143),new Vector2Int(320,180),new Vector2Int(0,180));
            Rect(39,99,9,42,"1a2a3c"); Rect(37,97,12,3,"293e50"); Rect(40,103,1,30,"2a3e50"); Rect(43,89,2,8,"273c4f");
            Rect(273,92,11,49,"182b3b"); Rect(272,90,14,3,"2a4050"); Rect(276,96,1,35,"2a4050");
            backdrops[palette] = (Color32[])buffer.Clone();
            }
            for (int i = 0; i < 14; i++) Rect((i * 193 + time * (1 + i % 3)) % 327,130 + Math.Sin(time * .6 + i) * 23,1,1,"d9edb7",.2);
        }
        void Terrain(Block b, int color, bool active, double alpha = .32)
        {
            for (double y = b.y; y < b.y + b.h; y += 8) for (double x = b.x; x < b.x + b.w; x += 8)
            {
                if (active)
                {
                    Rect(x,y,8,8,Fills[color]); Rect(x,y,8,1,Highlights[color]); Rect(x,y+7,8,1,Edges[color]);
                    if (color == 4) Rect(x+2,y+3,4,2,"8f7c47");
                    else { Rect(x,y+1,1,6,Edges[color]); Rect(x+3,y+3,2,1,Edges[color]); Rect(x+6,y+5,1,1,Edges[color]); }
                }
                else
                { Rect(x,y,8,1,Highlights[color],alpha); Rect(x,y+7,8,1,Highlights[color],alpha); Rect(x,y,1,8,Highlights[color],alpha); Rect(x+7,y,1,8,Highlights[color],alpha); Rect(x+3,y+3,1,1,Highlights[color],alpha); }
            }
        }
        public void Draw(PhaseController s)
        {
            Background(s.phase, s.time);
            for (int i = 0; i < s.blocks.Count; i++)
            { var b = s.blocks[i]; Terrain(b, b.phase < 0 ? 3 : b.phase, (b.phase < 0 || b.phase == s.phase) && !s.blocked.Contains(i), s.blocked.Contains(i) ? .55 : .32); }
            if (s.mover != null)
            {
                var m = s.mover;
                for (int i = 0; i < (m.returns ? 1 : m.nodes.Length); i++)
                {
                    var a = m.nodes[i]; var b = m.nodes[(i + 1) % m.nodes.Length];
                    double length = Math.Max(Math.Abs(b[0] - a[0]), Math.Abs(b[1] - a[1]));
                    for (int n = 0; n < length; n++) if (n % 4 < 2) Rect(a[0] + m.w / 2 + (b[0] - a[0]) * n / length,a[1] + m.h / 2 + (b[1] - a[1]) * n / length,1,1,"e0c569",.5);
                }
                foreach (var n in m.nodes) Rect(n[0] + m.w / 2 - 1,n[1] + 2,3,3,"e0c569",.5);
                Terrain(m, 4, true);
            }
            foreach (var t in s.trails) Rect(t.x,t.y,8,11,Highlights[t.phase],t.life*2);
            if (s.respawn <= 0) DrawPlayer(s.player, s.time);
            foreach (var p in s.particles) Rect(p.x,p.y,1,1,p.phase < 0 ? "dce7ee" : Highlights[p.phase],Math.Min(1,p.life*3));
            if (s.flash > 0) Rect(0,0,320,180,Highlights[s.phase],s.flash*.3);
            Texture.SetPixels32(buffer); Texture.Apply(false);
        }
        void DrawPlayer(Player p, double time)
        {
            int x = (int)Math.Floor(p.x+.5), feet = (int)Math.Floor(p.y+p.h+.5), y = feet - (p.ducking ? 8 : 15);
            string hair = p.dash != 0 ? "e9a0a4" : "7cbfe2";
            int run = p.grounded && Math.Abs(p.vx)>7 ? (int)Math.Floor(Math.Sin(time*23)+.5) : 0;
            if (p.climbing)
            {
                int step = p.climbMove != 0 ? (int)Math.Floor(Math.Sin(time*(p.climbMove<0?12:18))+.5) : 0;
                bool tired = p.stamina<=20 && (int)Math.Floor(time*8)%2!=0;
                Rect(x+1,y+1,7,4,tired?"ff8f91":hair); Rect(x+1,y+4,6,3,"f2d7b5"); Rect(x+1,y+7,6,5,"80b9b8");
                Rect(x+(p.facing>0?6:0),y+3+step,2,5,"f2d7b5"); Rect(x+1,feet-4+step,2,3,"343f53"); Rect(x+5,feet-4-step,2,3,"343f53"); return;
            }
            Rect(x+(p.facing>0?-3:7),y+7,4,2,hair); Rect(x+1,y,7,4,hair); Rect(x,y+3,7,4,"f2d7b5"); Rect(x+(p.facing>0?6:1),y+4,1,1,"243043");
            Rect(x+1,y+7,7,p.ducking?1:5,"80b9b8"); if (!p.ducking) Rect(x,y+8,2,4,"718b91");
            Rect(x+1,feet-3+run,2,3-run,"343f53"); Rect(x+5,feet-3-run,2,3+run,"343f53");
        }
        public void Dispose() { UnityEngine.Object.Destroy(Texture); }
    }
}
