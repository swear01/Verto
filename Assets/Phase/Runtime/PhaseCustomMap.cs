using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Phase
{
    public static class PhaseCustomMap
    {
        public const int Width = 320, Height = 176, MaxJsonLength = 200000;
        public static PhaseDemoRoom Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaxJsonLength)
                throw new ArgumentException("地圖檔案過大或空白。");
            try
            {
                var data = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                CheckFields(data, new[] { "name", "blocks", "spikes", "lasers", "crystals", "platforms", "gears", "spawnX", "spawnY" });
                CheckNumbers(data, "spawnX", "spawnY");
                if (data["name"] != null && (data["name"].Type != JTokenType.String || data["name"].Value<string>().Length > 64))
                    throw new ArgumentException("地圖名稱最多64字元。");
                if (!(data["blocks"] is JArray blocks) || new[] { "spikes", "lasers", "crystals", "platforms", "gears" }.Any(f => data[f] != null && !(data[f] is JArray)))
                    throw new ArgumentException("地形、尖刺、雷射、水晶、平台與齒輪必須是陣列。");
                JArray spikes = data["spikes"] as JArray, lasers = data["lasers"] as JArray, crystals = data["crystals"] as JArray;
                if (blocks.Count + (spikes?.Count ?? 0) + (lasers?.Count ?? 0) + (crystals?.Count ?? 0) + ((data["platforms"] as JArray)?.Count ?? 0) + ((data["gears"] as JArray)?.Count ?? 0) > 880)
                    throw new ArgumentException("地圖超過880格。");
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
                    CheckNumbers(crystal, "x", "y", "kind", "phaseMask");
                }
                var room = new PhaseDemoRoom {
                    name=data["name"]?.Value<string>(),spawnX=data["spawnX"].Value<double>(),spawnY=data["spawnY"].Value<double>(),
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
                        x=c["x"].Value<double>(),y=c["y"].Value<double>(),kind=(CrystalKind)c["kind"].Value<int>(),phaseMask=c["phaseMask"].Value<int>()
                    }).ToArray()
                };
                ReadMechanisms(data, room);
                Validate(room); return room;
            }
            catch (JsonException error) { throw new ArgumentException("地圖JSON格式無效。", error); }
            catch (OverflowException error) { throw new ArgumentException("地圖數值超出範圍。", error); }
        }
        // Platforms and gears also persist in the editor draft, which may not be a valid map yet.
        internal static void ReadMechanisms(JObject data, PhaseDemoRoom room)
        {
            if (new[] { "platforms", "gears" }.Any(f => data[f] != null && !(data[f] is JArray)))
                throw new ArgumentException("平台與齒輪必須是陣列。");
            room.platforms = (data["platforms"] as JArray)?.Select(item =>
            {
                var platform = item as JObject;
                CheckFields(platform, new[] { "w", "h", "nodeX", "nodeY", "returns" });
                CheckNumbers(platform, "w", "h");
                if (platform["returns"] != null && platform["returns"].Type != JTokenType.Boolean) throw new ArgumentException("平台returns必須是布林值。");
                return new DemoPlatform { w = platform["w"].Value<double>(), h = platform["h"].Value<double>(),
                    nodeX = Numbers(platform["nodeX"], "nodeX"), nodeY = Numbers(platform["nodeY"], "nodeY"), returns = platform["returns"]?.Value<bool>() ?? false };
            }).ToArray();
            room.gears = (data["gears"] as JArray)?.Select(item =>
            {
                var gear = item as JObject;
                CheckFields(gear, new[] { "wall", "platform", "top", "endX" });
                CheckNumbers(gear, "top", "endX");
                Block Box(JToken token)
                {
                    var box = token as JObject;
                    CheckFields(box, new[] { "x", "y", "w", "h" });
                    CheckNumbers(box, "x", "y", "w", "h");
                    return new Block(box["x"].Value<double>(), box["y"].Value<double>(), box["w"].Value<double>(), box["h"].Value<double>());
                }
                return new DemoGear { wall = Box(gear["wall"]), platform = Box(gear["platform"]), top = gear["top"].Value<double>(), endX = gear["endX"].Value<double>() };
            }).ToArray();
        }
        internal static PhaseDemoRoom ReadMechanisms(string json)
        {
            try { var room = new PhaseDemoRoom(); ReadMechanisms(JObject.Parse(json), room); return room; }
            catch (JsonException error) { throw new ArgumentException("地圖JSON格式無效。", error); }
            catch (OverflowException error) { throw new ArgumentException("地圖數值超出範圍。", error); }
        }
        internal static string MechanismJson(PhaseDemoRoom room) => WriteMechanisms(room, new JObject()).ToString(Formatting.None);
        static JObject WriteMechanisms(PhaseDemoRoom room, JObject data)
        {
            JObject Box(Block b) => new JObject { ["x"] = (int)b.x, ["y"] = (int)b.y, ["w"] = (int)b.w, ["h"] = (int)b.h };
            if (room.platforms?.Length > 0) data["platforms"] = new JArray(room.platforms.Select(p => new JObject {
                ["w"] = (int)p.w, ["h"] = (int)p.h, ["nodeX"] = new JArray(p.nodeX.Select(x => (int)x)), ["nodeY"] = new JArray(p.nodeY.Select(y => (int)y)), ["returns"] = p.returns }));
            if (room.gears?.Length > 0) data["gears"] = new JArray(room.gears.Select(g => new JObject {
                ["wall"] = Box(g.wall), ["platform"] = Box(g.platform), ["top"] = (int)g.top, ["endX"] = (int)g.endX }));
            return data;
        }
        static double[] Numbers(JToken token, string field)
        {
            if (!(token is JArray values)) throw new ArgumentException("地圖欄位必須是數值陣列：" + field);
            return values.Select(value => Number(value, field)).ToArray();
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
            if (room.blocks.Length + (room.spikes?.Length ?? 0) + (room.lasers?.Length ?? 0) + (room.crystals?.Length ?? 0) + (room.platforms?.Length ?? 0) + (room.gears?.Length ?? 0) > 880)
                throw new ArgumentException("地圖超過880格。");
            if ((room.nodeX?.Length ?? 0) != 0 || (room.nodeY?.Length ?? 0) != 0 || room.hasGear ||
                room.gearWall != null || room.gearPlatform != null || room.gearTop != 0 || room.gearEndX != 0 || room.returns ||
                double.IsNaN(room.moverW) || double.IsInfinity(room.moverW) || double.IsNaN(room.moverH) || double.IsInfinity(room.moverH))
                throw new ArgumentException("自訂地圖的平台與齒輪必須使用platforms與gears陣列。");
            if (!Aligned(room.spawnX) || !Aligned(room.spawnY) || room.spawnX < 0 || room.spawnX > 312 || room.spawnY < 0 || room.spawnY > 160)
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
                if (emitter.ActiveInPhase(0) && PhaseController.Overlap(player, emitter.Beam(solids, Width)))
                    throw new ArgumentException("出生點位於紅色模式的雷射光束上。");
            }
            foreach (var crystal in room.crystals ?? Array.Empty<DemoCrystal>())
            {
                if (crystal == null || (int)crystal.kind < 0 || (int)crystal.kind > 1 || crystal.phaseMask < 1 || crystal.phaseMask > 7)
                    throw new ArgumentException("水晶種類或顏色無效。");
                AddCells(crystal.x, crystal.y, 8, 8, occupied);
            }
            // Mechanisms occupy cells at their start; every stop and the gear's travel limits stay inside the map.
            foreach (var platform in room.platforms ?? Array.Empty<DemoPlatform>())
            {
                MovingPlatform mover;
                try { mover = platform?.Create() ?? throw new ArgumentException(); }
                catch (ArgumentException) { throw new ArgumentException("平台路線無效。"); }
                for (int i = 1; i < platform.nodeX.Length; i++) Inside(platform.nodeX[i], platform.nodeY[i], platform.w, platform.h);
                AddCells(mover.x, mover.y, mover.w, mover.h, occupied);
                if (PhaseController.Overlap(player, mover)) throw new ArgumentException("出生點與平台重疊。");
            }
            foreach (var gear in room.gears ?? Array.Empty<DemoGear>())
            {
                ClimbGear drive;
                try { drive = gear?.Create() ?? throw new ArgumentException(); }
                catch (ArgumentException) { throw new ArgumentException("齒輪行程無效。"); }
                AddCells(drive.wall.x, drive.wall.y, drive.wall.w, drive.wall.h, occupied);
                AddCells(drive.platform.x, drive.platform.y, drive.platform.w, drive.platform.h, occupied);
                Inside(drive.wall.x, drive.top, drive.wall.w, drive.wall.h);
                Inside(drive.endX, drive.platform.y, drive.platform.w, drive.platform.h);
                if (PhaseController.Overlap(player, drive.wall) || PhaseController.Overlap(player, drive.platform)) throw new ArgumentException("出生點與齒輪重疊。");
            }
        }
        // The custom-map wire schema; lasers, crystals, platforms and gears appear only when used.
        public static string ToJson(PhaseDemoRoom room)
        {
            var data = new JObject {
                ["name"] = room.name,
                ["blocks"] = new JArray(room.blocks.Select(b => new JObject { ["x"] = (int)b.x, ["y"] = (int)b.y, ["w"] = (int)b.w, ["h"] = (int)b.h, ["phase"] = b.phase, ["phaseMask"] = b.phaseMask })),
                ["spikes"] = new JArray((room.spikes ?? Array.Empty<DemoSpike>()).Select(s => new JObject { ["x"] = (int)s.x, ["y"] = (int)s.y, ["length"] = (int)s.length, ["direction"] = (int)s.direction, ["phaseMask"] = s.phaseMask }))
            };
            if (room.lasers?.Length > 0) data["lasers"] = new JArray(room.lasers.Select(l => new JObject { ["x"] = (int)l.x, ["y"] = (int)l.y, ["direction"] = (int)l.direction, ["phaseMask"] = l.phaseMask }));
            if (room.crystals?.Length > 0) data["crystals"] = new JArray(room.crystals.Select(c => new JObject { ["x"] = (int)c.x, ["y"] = (int)c.y, ["kind"] = (int)c.kind, ["phaseMask"] = c.phaseMask }));
            WriteMechanisms(room, data);
            data["spawnX"] = (int)room.spawnX; data["spawnY"] = (int)room.spawnY;
            return data.ToString(Formatting.Indented);
        }
        static bool Aligned(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value % 8 == 0;
        static void Inside(double x, double y, double width, double height)
        {
            if (!Aligned(x) || !Aligned(y) || !Aligned(width) || !Aligned(height) || x < 0 || y < 0 || width < 8 || height < 8 || x + width > Width || y + height > Height)
                throw new ArgumentException("物件必須對齊8像素格線，且位於320×176範圍內。");
        }
        static void AddCells(double x, double y, double width, double height, HashSet<(int,int)> occupied)
        {
            Inside(x, y, width, height);
            for (int cy = (int)y / 8; cy < (y + height) / 8; cy++)
                for (int cx = (int)x / 8; cx < (x + width) / 8; cx++)
                    if (!occupied.Add((cx,cy))) throw new ArgumentException("同一格不能放置重疊物件。");
        }
    }
}
