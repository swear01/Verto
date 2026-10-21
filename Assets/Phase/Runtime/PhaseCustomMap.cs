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
        public const int Width = 320, Height = 176, MaxWidth = 1280, MaxHeight = 720, MaxCells = MaxWidth / 8 * (MaxHeight / 8), MaxJsonLength = 2000000, MaxNumber = 1000000;
        // The map file format: maps without a version are v1. Saving always writes the latest.
        public const int Version = 2;
        public const string Format = "verto-map", Generator = "Verto 0.2";
        static readonly string[] Directions = { "up", "down", "left", "right" };
        // Upgrades[n] turns a version n+1 file into version n+2.
        static readonly Func<JObject, JObject>[] Upgrades = { data => ToData(ReadV1(data)) };
        // A draft may be unfinished, so it loads without the playability checks.
        public static PhaseDemoRoom Parse(string json, bool validate = true)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaxJsonLength)
                throw new ArgumentException("地圖檔案過大或空白。");
            try
            {
                var data = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                int version = 1;
                if (data["version"] != null || data["format"] != null)
                {
                    if (data["format"]?.Type != JTokenType.String || data["format"].Value<string>() != Format) throw new ArgumentException("這不是Verto地圖檔案。");
                    version = (int)Number(data["version"], "version");
                    if (version < 1) throw new ArgumentException("地圖版本無效。");
                }
                if (version > Version) throw new ArgumentException($"這份地圖由較新的Verto製作（地圖格式第{version}版，本遊戲支援到第{Version}版），請先更新遊戲。");
                for (; version < Version; version++)
                {
                    if (version > Upgrades.Length) throw new ArgumentException($"找不到地圖格式第{version}版的升級方式。");
                    data = Upgrades[version - 1](data);
                }
                var room = FromData(data);
                if (validate) Validate(room);
                return room;
            }
            catch (JsonException error) { throw new ArgumentException("地圖JSON格式無效。", error); }
            catch (OverflowException error) { throw new ArgumentException("地圖數值超出範圍。", error); }
        }
        // The version 1 file: rectangles of terrain, spikes with a length in pixels, and cell positions in pixels.
        static PhaseDemoRoom ReadV1(JObject data)
        {
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
            return room;
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
            // Far beyond any room, so later cell and pixel arithmetic cannot wrap.
            if (Math.Abs(number) > MaxNumber) throw new ArgumentException("地圖數值超出範圍：" + field);
            return number;
        }
        public static void Validate(PhaseDemoRoom room)
        {
            if (room == null || room.blocks == null || room.blocks.Length == 0)
                throw new ArgumentException("地圖至少需要一格地形。");
            int w = room.GridWidth, h = room.GridHeight;
            if (w < Width || w > MaxWidth || h < Height || h > MaxHeight || w % 8 != 0 || h % 8 != 0)
                throw new ArgumentException($"地圖大小必須是8的倍數：寬{Width}–{MaxWidth}、高{Height}–{MaxHeight}。");
            if (room.blocks.Length + (room.spikes?.Length ?? 0) + (room.lasers?.Length ?? 0) + (room.crystals?.Length ?? 0) + (room.platforms?.Length ?? 0) > w / 8 * (h / 8))
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
                if (block.spiked && block.ActiveInPhase(0) && PhaseController.Overlap(player, PhaseController.Reach(block)))
                    throw new ArgumentException("出生點碰到紅色模式的尖刺覆蓋地形。");
            }
            foreach (var item in room.platforms ?? Array.Empty<DemoPlatform>())
            {
                var platform = CheckPlatform(item, w, h);
                AddCells(platform.x, platform.y, platform.w, platform.h, occupied);
                foreach (var teeth in platform.attached)
                {
                    AddCells(teeth.x, teeth.y, teeth.w, teeth.h, occupied);
                    if (teeth.ActiveInPhase(0) && PhaseController.Overlap(player, teeth.Hitbox)) throw new ArgumentException("出生點與紅色模式的尖刺重疊。");
                }
                if (platform.ActiveInPhase(0) && PhaseController.Overlap(player, platform.spiked ? PhaseController.Reach(platform) : platform))
                    throw new ArgumentException("出生點與紅色模式的平台重疊。");
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
            var solids = room.blocks.Concat<Block>((room.platforms ?? Array.Empty<DemoPlatform>()).Select(p => p.Create()))
                .Where(b => b.ActiveInPhase(0)).ToArray();
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
        public static string ToJson(PhaseDemoRoom room) => ToData(room).ToString(Formatting.Indented);
        static JArray Cell(double x, double y) => new JArray((int)x / 8, (int)y / 8);
        // The latest file: terrain as rows of cells, everything else as typed objects.
        static JObject ToData(PhaseDemoRoom room)
        {
            int w = room.GridWidth / 8, h = room.GridHeight / 8;
            if (!Aligned(room.spawnX) || !Aligned(room.spawnY)) throw new ArgumentException("出生點必須位於格線內。");
            var rows = Enumerable.Range(0, h).Select(_ => Enumerable.Repeat('.', w).ToArray()).ToArray();
            foreach (var block in room.blocks)
            {
                if (block == null || block.phase < -1 || block.phase > 2 || block.phaseMask < 1 || block.phaseMask > 7) throw new ArgumentException("地形顏色必須有效。");
                InsideRoom(block.x, block.y, block.w, block.h, room.GridWidth, room.GridHeight);
                for (int cy = (int)block.y / 8; cy < (block.y + block.h) / 8; cy++)
                    for (int cx = (int)block.x / 8; cx < (block.x + block.w) / 8; cx++)
                    {
                        if (rows[cy][cx] != '.') throw new ArgumentException("同一格不能放置重疊物件。");
                        rows[cy][cx] = (char)((block.spiked ? 'A' : '1') + block.phaseMask - 1);
                    }
            }
            var objects = new JArray();
            foreach (var spike in room.spikes ?? Array.Empty<DemoSpike>())
            {
                if (spike == null || (int)spike.direction < 0 || (int)spike.direction > 3 || spike.phaseMask < 1 || spike.phaseMask > 7 || !Aligned(spike.length) || spike.length < 8)
                    throw new ArgumentException("尖刺方向、顏色或長度無效。");
                bool across = spike.direction == SpikeDirection.Up || spike.direction == SpikeDirection.Down;
                InsideRoom(spike.x, spike.y, across ? spike.length : 8, across ? 8 : spike.length, room.GridWidth, room.GridHeight);
                for (int i = 0; i < spike.length / 8; i++)
                    objects.Add(new JObject { ["type"] = "spike", ["cell"] = Cell(spike.x + (across ? i * 8 : 0), spike.y + (across ? 0 : i * 8)), ["dir"] = Directions[(int)spike.direction], ["mask"] = spike.phaseMask });
            }
            foreach (var laser in room.lasers ?? Array.Empty<DemoLaser>())
            {
                if (laser == null || (int)laser.direction < 0 || (int)laser.direction > 3 || laser.phaseMask < 1 || laser.phaseMask > 7) throw new ArgumentException("雷射方向或顏色無效。");
                InsideRoom(laser.x, laser.y, 8, 8, room.GridWidth, room.GridHeight);
                objects.Add(new JObject { ["type"] = "laser", ["cell"] = Cell(laser.x, laser.y), ["dir"] = Directions[(int)laser.direction], ["mask"] = laser.phaseMask });
            }
            foreach (var crystal in room.crystals ?? Array.Empty<DemoCrystal>())
            {
                if (crystal == null || crystal.phaseMask < 1 || crystal.phaseMask > 7) throw new ArgumentException("水晶顏色無效。");
                InsideRoom(crystal.x, crystal.y, 8, 8, room.GridWidth, room.GridHeight);
                objects.Add(new JObject { ["type"] = "crystal", ["cell"] = Cell(crystal.x, crystal.y), ["mask"] = crystal.phaseMask });
            }
            foreach (var platform in room.platforms ?? Array.Empty<DemoPlatform>())
            {
                CheckPlatform(platform, room.GridWidth, room.GridHeight);
                objects.Add(new JObject {
                    ["type"] = "platform", ["orientation"] = platform.vertical ? "vertical" : "horizontal", ["length"] = platform.length,
                    ["pivots"] = new JArray(platform.pivots.Select(p => new JArray(p.x, p.y))), ["reverse"] = platform.reverse, ["mask"] = platform.phaseMask,
                    ["spiked"] = platform.spiked, ["spikes"] = new JArray(platform.spikes.Select(d => Directions[(int)d]))
                });
            }
            return new JObject {
                ["format"] = Format, ["version"] = Version, ["generator"] = Generator, ["name"] = room.name,
                ["size"] = new JArray(w, h), ["spawn"] = Cell(room.spawnX, room.spawnY),
                ["terrain"] = new JArray(rows.Select(r => new string(r))), ["objects"] = objects
            };
        }
        static PhaseDemoRoom FromData(JObject data)
        {
            CheckFields(data, new[] { "format", "version", "generator", "name", "size", "spawn", "terrain", "objects" });
            if (data["name"] != null && data["name"].Type != JTokenType.Null && (data["name"].Type != JTokenType.String || data["name"].Value<string>().Length > 64))
                throw new ArgumentException("地圖名稱最多64字元。");
            var size = Pair(data["size"], "size");
            if (size.x < Width / 8 || size.x > MaxWidth / 8 || size.y < Height / 8 || size.y > MaxHeight / 8)
                throw new ArgumentException($"地圖大小必須介於{Width / 8}×{Height / 8}與{MaxWidth / 8}×{MaxHeight / 8}格。");
            var spawn = Pair(data["spawn"], "spawn");
            if (!(data["terrain"] is JArray terrain) || terrain.Count > size.y || terrain.Any(r => r.Type != JTokenType.String || r.Value<string>().Length > size.x))
                throw new ArgumentException("地形必須是不超過地圖大小的字串陣列。");
            if (data["objects"] != null && !(data["objects"] is JArray)) throw new ArgumentException("物件必須是陣列。");
            var blocks = new List<Block>(); var spikes = new List<DemoSpike>(); var lasers = new List<DemoLaser>(); var crystals = new List<DemoCrystal>(); var platforms = new List<DemoPlatform>();
            for (int y = 0; y < terrain.Count; y++)
            {
                string row = terrain[y].Value<string>();
                for (int x = 0; x < row.Length; x++)
                {
                    char c = row[x];
                    if (c == '.') continue;
                    bool spiked = c >= 'A' && c <= 'G';
                    int mask = spiked ? c - 'A' + 1 : c - '0';
                    if (mask < 1 || mask > 7) throw new ArgumentException("地形包含無法辨識的字元：" + c);
                    blocks.Add(new Block(x * 8, y * 8, 8, 8, -1, mask) { spiked = spiked });
                }
            }
            foreach (var item in data["objects"] ?? new JArray())
            {
                var obj = item as JObject;
                string type = obj?["type"]?.Type == JTokenType.String ? obj["type"].Value<string>() : null;
                switch (type)
                {
                    case "spike":
                        CheckFields(obj, new[] { "type", "cell", "dir", "mask" });
                        var at = Pair(obj["cell"], "cell");
                        spikes.Add(new DemoSpike { x = at.x * 8, y = at.y * 8, length = 8, direction = (SpikeDirection)Direction(obj["dir"]), phaseMask = Mask(obj) });
                        break;
                    case "laser":
                        CheckFields(obj, new[] { "type", "cell", "dir", "mask" });
                        at = Pair(obj["cell"], "cell");
                        lasers.Add(new DemoLaser { x = at.x * 8, y = at.y * 8, direction = (LaserDirection)Direction(obj["dir"]), phaseMask = Mask(obj) });
                        break;
                    case "crystal":
                        CheckFields(obj, new[] { "type", "cell", "mask" });
                        at = Pair(obj["cell"], "cell");
                        crystals.Add(new DemoCrystal { x = at.x * 8, y = at.y * 8, phaseMask = Mask(obj) });
                        break;
                    case "platform":
                        CheckFields(obj, new[] { "type", "orientation", "length", "pivots", "reverse", "mask", "spiked", "spikes" });
                        string orientation = obj["orientation"]?.Type == JTokenType.String ? obj["orientation"].Value<string>() : null;
                        if (orientation != "horizontal" && orientation != "vertical") throw new ArgumentException("平台方向必須是 horizontal 或 vertical。");
                        if (!(obj["pivots"] is JArray stops) || (obj["spikes"] != null && !(obj["spikes"] is JArray))) throw new ArgumentException("平台的路徑與尖刺必須是陣列。");
                        platforms.Add(new DemoPlatform {
                            vertical = orientation == "vertical", length = (int)Number(obj["length"], "length"), phaseMask = Mask(obj),
                            reverse = Flag(obj, "reverse"), spiked = Flag(obj, "spiked"),
                            pivots = stops.Select(p => Pair(p, "pivots")).ToArray(),
                            spikes = (obj["spikes"] ?? new JArray()).Select(d => (SpikeDirection)Direction(d)).ToArray()
                        });
                        break;
                    default: throw new ArgumentException("地圖包含不支援的物件：" + (type ?? "未知"));
                }
            }
            return new PhaseDemoRoom {
                name = data["name"]?.Type == JTokenType.String ? data["name"].Value<string>() : null, width = size.x * 8, height = size.y * 8,
                spawnX = spawn.x * 8, spawnY = spawn.y * 8, blocks = blocks.ToArray(), spikes = spikes.ToArray(),
                lasers = lasers.Count > 0 ? lasers.ToArray() : null, crystals = crystals.Count > 0 ? crystals.ToArray() : null, platforms = platforms.Count > 0 ? platforms.ToArray() : null
            };
        }
        static (int x, int y) Pair(JToken value, string field)
        {
            if (!(value is JArray pair) || pair.Count != 2) throw new ArgumentException("地圖欄位必須是兩個整數：" + field);
            return ((int)Number(pair[0], field), (int)Number(pair[1], field));
        }
        static int Direction(JToken value)
        {
            int index = value?.Type == JTokenType.String ? Array.IndexOf(Directions, value.Value<string>()) : -1;
            if (index < 0) throw new ArgumentException("方向必須是 up、down、left 或 right。");
            return index;
        }
        static int Mask(JObject obj) => (int)Number(obj["mask"], "mask");
        static bool Flag(JObject obj, string field)
        {
            if (obj[field] == null) return false;
            if (obj[field].Type != JTokenType.Boolean) throw new ArgumentException("地圖欄位必須是布林值：" + field);
            return obj[field].Value<bool>();
        }
        // A platform that can be built: odd length, two or more pivots, valid color and sides. The path may cross other
        // objects, but every stop keeps the body and its spike rows inside the room.
        public static Platform CheckPlatform(DemoPlatform item, int roomWidth, int roomHeight)
        {
            if (item == null || item.pivots == null || item.spikes == null || item.pivots.Length < 2 || item.length < 1 || item.length % 2 == 0 || item.phaseMask < 1 || item.phaseMask > 7 ||
                item.spikes.Any(d => (int)d < 0 || (int)d > 3))
                throw new ArgumentException("平台的長度、路徑、方向或顏色無效。");
            var platform = item.Create();
            for (int i = 0; i < platform.pivots.Length; i++)
            {
                var at = platform.OriginAt(i); var home = platform.OriginAt(0);
                InsideRoom(at.x, at.y, platform.w, platform.h, roomWidth, roomHeight);
                foreach (var teeth in platform.attached) InsideRoom(teeth.x + at.x - home.x, teeth.y + at.y - home.y, teeth.w, teeth.h, roomWidth, roomHeight);
            }
            return platform;
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
