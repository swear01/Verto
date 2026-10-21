using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Phase
{
    public static class PhaseCustomMap
    {
        // The smallest room is one 320×176 screen; a larger room scrolls. MaxCells bounds the largest.
        public const int Width = 320, Height = 176, MaxWidth = 1280, MaxHeight = 720, MaxCells = MaxWidth / 8 * (MaxHeight / 8), MaxJsonLength = 2000000;
        public static PhaseDemoRoom Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaxJsonLength)
                throw new ArgumentException("地圖檔案過大或空白。");
            try
            {
                var data = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                CheckFields(data, new[] { "name", "width", "height", "blocks", "spikes", "lasers", "crystals", "spawnX", "spawnY" });
                CheckNumbers(data, "spawnX", "spawnY");
                if (data["name"] != null && (data["name"].Type != JTokenType.String || data["name"].Value<string>().Length > 64))
                    throw new ArgumentException("地圖名稱最多64字元。");
                if (!(data["blocks"] is JArray blocks) || new[] { "spikes", "lasers", "crystals" }.Any(f => data[f] != null && !(data[f] is JArray)))
                    throw new ArgumentException("地形、尖刺、雷射與水晶必須是陣列。");
                JArray spikes = data["spikes"] as JArray, lasers = data["lasers"] as JArray, crystals = data["crystals"] as JArray;
                if (blocks.Count + (spikes?.Count ?? 0) + (lasers?.Count ?? 0) + (crystals?.Count ?? 0) > MaxCells)
                    throw new ArgumentException("地圖格數過多。");
                foreach (var item in blocks)
                {
                    var block = item as JObject;
                    CheckFields(block, new[] { "x", "y", "w", "h", "phase", "phaseMask" });
                    CheckNumbers(block, "x", "y", "w", "h", "phaseMask");
                    if (block["phase"] != null) CheckNumbers(block, "phase");
                }
                foreach (var item in spikes ?? new JArray())
                {
                    var spike = item as JObject;
                    CheckFields(spike, new[] { "x", "y", "length", "direction", "phaseMask" });
                    CheckNumbers(spike, "x", "y", "length", "direction", "phaseMask");
                }
                foreach (var item in lasers ?? new JArray())
                {
                    var laser = item as JObject;
                    CheckFields(laser, new[] { "x", "y", "direction", "phaseMask" });
                    CheckNumbers(laser, "x", "y", "direction", "phaseMask");
                }
                foreach (var item in crystals ?? new JArray())
                {
                    var crystal = item as JObject;
                    CheckFields(crystal, new[] { "x", "y", "kind", "phaseMask" });
                    CheckNumbers(crystal, "x", "y", "phaseMask");
                    // Older maps tag each crystal with a kind; only the jump crystal (0) remains.
                    if (crystal["kind"] != null && Number(crystal["kind"], "kind") != 0) throw new ArgumentException("只支援跳躍水晶。");
                }
                var room = new PhaseDemoRoom {
                    name=data["name"]?.Value<string>(),width=Size(data,"width",Width,MaxWidth),height=Size(data,"height",Height,MaxHeight),spawnX=data["spawnX"].Value<double>(),spawnY=data["spawnY"].Value<double>(),
                    blocks=blocks.Select(b=>new Block(b["x"].Value<double>(),b["y"].Value<double>(),b["w"].Value<double>(),b["h"].Value<double>(),
                        b["phase"]?.Value<int>() ?? -1,b["phaseMask"].Value<int>())).ToArray(),
                    spikes=(spikes ?? new JArray()).Select(s=>new DemoSpike {
                        x=s["x"].Value<double>(),y=s["y"].Value<double>(),length=s["length"].Value<double>(),
                        direction=(SpikeDirection)s["direction"].Value<int>(),phaseMask=s["phaseMask"].Value<int>()
                    }).ToArray(),
                    lasers=lasers?.Select(l=>new DemoLaser {
                        x=l["x"].Value<double>(),y=l["y"].Value<double>(),direction=(LaserDirection)l["direction"].Value<int>(),phaseMask=l["phaseMask"].Value<int>()
                    }).ToArray(),
                    crystals=crystals?.Select(c=>new DemoCrystal {
                        x=c["x"].Value<double>(),y=c["y"].Value<double>(),phaseMask=c["phaseMask"].Value<int>()
                    }).ToArray()
                };
                Validate(room); return room;
            }
            catch (JsonException error) { throw new ArgumentException("地圖JSON格式無效。", error); }
            catch (OverflowException error) { throw new ArgumentException("地圖數值超出範圍。", error); }
        }
        // An absent size is the one-screen default (0); a present one is a multiple of 8 within the limits.
        static int Size(JObject data, string field, int min, int max)
        {
            if (data[field] == null) return 0;
            double size = Number(data[field], field);
            if (size < min || size > max || size % 8 != 0) throw new ArgumentException($"地圖{field}必須是8的倍數，介於{min}與{max}。");
            return (int)size;
        }
        static void CheckFields(JObject data, string[] allowed)
        {
            if (data == null) throw new ArgumentException("地圖物件格式無效。");
            foreach (var property in data.Properties())
                if (Array.IndexOf(allowed, property.Name) < 0) throw new ArgumentException("地圖包含不支援的欄位：" + property.Name);
        }
        static void CheckNumbers(JObject data, params string[] fields)
        {
            foreach (var field in fields) Number(data[field], field);
        }
        static double Number(JToken value, string field)
        {
            if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float))
                throw new ArgumentException("地圖欄位必須是數值：" + field);
            double number = value.Value<double>();
            if (double.IsNaN(number) || double.IsInfinity(number) || number % 1 != 0)
                throw new ArgumentException("地圖欄位必須是有限整數：" + field);
            return number;
        }
        public static void Validate(PhaseDemoRoom room)
        {
            if (room == null || room.blocks == null || room.blocks.Length == 0)
                throw new ArgumentException("地圖至少需要一格地形。");
            int w = room.GridWidth, h = room.GridHeight;
            if (w < Width || w > MaxWidth || h < Height || h > MaxHeight || w % 8 != 0 || h % 8 != 0)
                throw new ArgumentException($"地圖大小必須是8的倍數：寬{Width}–{MaxWidth}、高{Height}–{MaxHeight}。");
            if (room.blocks.Length + (room.spikes?.Length ?? 0) + (room.lasers?.Length ?? 0) + (room.crystals?.Length ?? 0) > w / 8 * (h / 8))
                throw new ArgumentException($"地圖超過{w / 8 * (h / 8)}格。");
            void AddCells(double x, double y, double width, double height, HashSet<(int,int)> cells) => AddCellsIn(x, y, width, height, cells, w, h);
            if (!Aligned(room.spawnX) || !Aligned(room.spawnY) || room.spawnX < 0 || room.spawnX > w - 8 || room.spawnY < 0 || room.spawnY > h - 16)
                throw new ArgumentException("出生點必須位於格線內。");
            var occupied = new HashSet<(int,int)>();
            var player = new Player(room.spawnX, room.spawnY);
            foreach (var block in room.blocks)
            {
                if (block == null || block.phase < -1 || block.phase > 2 || block.phaseMask < 1 || block.phaseMask > 7)
                    throw new ArgumentException("地形顏色必須有效。");
                AddCells(block.x, block.y, block.w, block.h, occupied);
                if (block.ActiveInPhase(0) && PhaseController.Overlap(player, block))
                    throw new ArgumentException("出生點與紅色模式的實體地形重疊。");
            }
            foreach (var spike in room.spikes ?? Array.Empty<DemoSpike>())
            {
                if (spike == null || (int)spike.direction < 0 || (int)spike.direction > 3 || spike.phaseMask < 1 || spike.phaseMask > 7 || !Aligned(spike.length) || spike.length < 8)
                    throw new ArgumentException("尖刺方向、顏色或長度無效。");
                var hazard = new Spike(spike.x, spike.y, spike.length, spike.direction, spike.phaseMask);
                AddCells(hazard.x, hazard.y, hazard.w, hazard.h, occupied);
                if (hazard.ActiveInPhase(0) && PhaseController.Overlap(player, hazard.Hitbox))
                    throw new ArgumentException("出生點與紅色模式的尖刺重疊。");
            }
            var solids = room.blocks.Where(b => b.ActiveInPhase(0)).ToArray();
            foreach (var laser in room.lasers ?? Array.Empty<DemoLaser>())
            {
                if (laser == null || (int)laser.direction < 0 || (int)laser.direction > 3 || laser.phaseMask < 1 || laser.phaseMask > 7)
                    throw new ArgumentException("雷射方向或顏色無效。");
                AddCells(laser.x, laser.y, 8, 8, occupied);
                var emitter = new Laser(laser.x, laser.y, laser.direction, laser.phaseMask);
                if (emitter.ActiveInPhase(0) && PhaseController.Overlap(player, emitter.Beam(solids, w, Math.Max(180, h))))
                    throw new ArgumentException("出生點位於紅色模式的雷射光束上。");
            }
            foreach (var crystal in room.crystals ?? Array.Empty<DemoCrystal>())
            {
                if (crystal == null || crystal.phaseMask < 1 || crystal.phaseMask > 7)
                    throw new ArgumentException("水晶顏色無效。");
                AddCells(crystal.x, crystal.y, 8, 8, occupied);
            }
        }
        // The exported custom-map JSON; lasers and crystals appear only when used.
        public static string ToJson(PhaseDemoRoom room)
        {
            var data = new JObject { ["name"] = room.name };
            // A one-screen room omits its size, so older readers and files stay compatible.
            if (room.GridWidth != Width || room.GridHeight != Height) { data["width"] = room.GridWidth; data["height"] = room.GridHeight; }
            data["blocks"] = new JArray(room.blocks.Select(b => new JObject { ["x"] = (int)b.x, ["y"] = (int)b.y, ["w"] = (int)b.w, ["h"] = (int)b.h, ["phase"] = b.phase, ["phaseMask"] = b.phaseMask }));
            data["spikes"] = new JArray((room.spikes ?? Array.Empty<DemoSpike>()).Select(s => new JObject { ["x"] = (int)s.x, ["y"] = (int)s.y, ["length"] = (int)s.length, ["direction"] = (int)s.direction, ["phaseMask"] = s.phaseMask }));
            if (room.lasers?.Length > 0) data["lasers"] = new JArray(room.lasers.Select(l => new JObject { ["x"] = (int)l.x, ["y"] = (int)l.y, ["direction"] = (int)l.direction, ["phaseMask"] = l.phaseMask }));
            if (room.crystals?.Length > 0) data["crystals"] = new JArray(room.crystals.Select(c => new JObject { ["x"] = (int)c.x, ["y"] = (int)c.y, ["phaseMask"] = c.phaseMask }));
            data["spawnX"] = (int)room.spawnX; data["spawnY"] = (int)room.spawnY;
            return data.ToString(Formatting.Indented);
        }
        static bool Aligned(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value % 8 == 0;
        static void InsideRoom(double x, double y, double width, double height, int roomWidth, int roomHeight)
        {
            if (!Aligned(x) || !Aligned(y) || !Aligned(width) || !Aligned(height) || x < 0 || y < 0 || width < 8 || height < 8 || x + width > roomWidth || y + height > roomHeight)
                throw new ArgumentException($"物件必須對齊8像素格線，且位於{roomWidth}×{roomHeight}範圍內。");
        }
        static void AddCellsIn(double x, double y, double width, double height, HashSet<(int,int)> occupied, int roomWidth, int roomHeight)
        {
            InsideRoom(x, y, width, height, roomWidth, roomHeight);
            for (int cy = (int)y / 8; cy < (y + height) / 8; cy++)
                for (int cx = (int)x / 8; cx < (x + width) / 8; cx++)
                    if (!occupied.Add((cx,cy))) throw new ArgumentException("同一格不能放置重疊物件。");
        }
    }
}
