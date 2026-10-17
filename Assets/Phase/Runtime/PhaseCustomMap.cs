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
                CheckFields(data, new[] { "name", "blocks", "spikes", "spawnX", "spawnY" });
                CheckNumbers(data, "spawnX", "spawnY");
                if (data["name"] != null && (data["name"].Type != JTokenType.String || data["name"].Value<string>().Length > 64))
                    throw new ArgumentException("地圖名稱最多64字元。");
                if (!(data["blocks"] is JArray blocks) || (data["spikes"] != null && !(data["spikes"] is JArray)))
                    throw new ArgumentException("地形與尖刺必須是陣列。");
                var spikes = data["spikes"] as JArray;
                if (blocks.Count + (spikes?.Count ?? 0) > 880) throw new ArgumentException("地圖超過880格。");
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
                var room = new PhaseDemoRoom {
                    name=data["name"]?.Value<string>(),spawnX=data["spawnX"].Value<double>(),spawnY=data["spawnY"].Value<double>(),
                    blocks=blocks.Select(b=>new Block(b["x"].Value<double>(),b["y"].Value<double>(),b["w"].Value<double>(),b["h"].Value<double>(),
                        b["phase"]?.Value<int>() ?? -1,b["phaseMask"].Value<int>())).ToArray(),
                    spikes=(spikes ?? new JArray()).Select(s=>new DemoSpike {
                        x=s["x"].Value<double>(),y=s["y"].Value<double>(),length=s["length"].Value<double>(),
                        direction=(SpikeDirection)s["direction"].Value<int>(),phaseMask=s["phaseMask"].Value<int>()
                    }).ToArray()
                };
                Validate(room); return room;
            }
            catch (JsonException error) { throw new ArgumentException("地圖JSON格式無效。", error); }
            catch (OverflowException error) { throw new ArgumentException("地圖數值超出範圍。", error); }
        }
        static void CheckFields(JObject data, string[] allowed)
        {
            if (data == null) throw new ArgumentException("地圖物件格式無效。");
            foreach (var property in data.Properties())
                if (Array.IndexOf(allowed, property.Name) < 0) throw new ArgumentException("地圖包含不支援的欄位：" + property.Name);
        }
        static void CheckNumbers(JObject data, params string[] fields)
        {
            foreach (var field in fields)
            {
                var value = data[field];
                if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float))
                    throw new ArgumentException("地圖欄位必須是數值：" + field);
                double number = value.Value<double>();
                if (double.IsNaN(number) || double.IsInfinity(number) || number % 1 != 0)
                    throw new ArgumentException("地圖欄位必須是有限整數：" + field);
            }
        }
        public static void Validate(PhaseDemoRoom room)
        {
            if (room == null || room.blocks == null || room.blocks.Length == 0)
                throw new ArgumentException("地圖至少需要一格地形。");
            if (room.blocks.Length + (room.spikes?.Length ?? 0) > 880)
                throw new ArgumentException("地圖超過880格。");
            if ((room.crystals?.Length ?? 0) != 0 || (room.lasers?.Length ?? 0) != 0 || (room.nodeX?.Length ?? 0) != 0 || (room.nodeY?.Length ?? 0) != 0 || room.hasGear ||
                room.gearWall != null || room.gearPlatform != null || room.gearTop != 0 || room.gearEndX != 0 || room.returns ||
                double.IsNaN(room.moverW) || double.IsInfinity(room.moverW) || double.IsNaN(room.moverH) || double.IsInfinity(room.moverH))
                throw new ArgumentException("自訂地圖目前支援靜態地形與尖刺。");
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
        }
        static bool Aligned(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value % 8 == 0;
        static void AddCells(double x, double y, double width, double height, HashSet<(int,int)> occupied)
        {
            if (!Aligned(x) || !Aligned(y) || !Aligned(width) || !Aligned(height) || x < 0 || y < 0 || width < 8 || height < 8 || x + width > Width || y + height > Height)
                throw new ArgumentException("地形與尖刺必須對齊8像素格線，且位於320×176範圍內。");
            for (int cy = (int)y / 8; cy < (y + height) / 8; cy++)
                for (int cx = (int)x / 8; cx < (x + width) / 8; cx++)
                    if (!occupied.Add((cx,cy))) throw new ArgumentException("同一格不能放置重疊物件。");
        }
    }
}
