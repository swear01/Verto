using System;
using System.Collections.Generic;

namespace Phase
{
    public static class PhaseCustomMap
    {
        public const int Width = 320, Height = 176, MaxJsonLength = 200000;
        public static void Validate(PhaseDemoRoom room)
        {
            if (room == null || room.blocks == null || room.blocks.Length == 0)
                throw new ArgumentException("地圖至少需要一格地形。");
            if (room.blocks.Length + (room.spikes?.Length ?? 0) > 880)
                throw new ArgumentException("地圖超過880格。");
            if ((room.nodeX?.Length ?? 0) != 0 || (room.nodeY?.Length ?? 0) != 0 || room.hasGear ||
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
