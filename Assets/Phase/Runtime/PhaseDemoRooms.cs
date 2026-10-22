using System;
using System.Collections.Generic;
using System.Linq;

namespace Phase
{
    [Serializable]
    public sealed class PhaseDemoRooms
    {
        public PhaseDemoRoom[] rooms;
    }

    [Serializable]
    public sealed class PhaseDemoRoom
    {
        public string name;
        public Block[] blocks;
        public double spawnX, spawnY;
        // A world room sits at a screen position (may be negative); more spawns and one goal are optional.
        public int atX, atY;
        public DemoPoint[] extraSpawns;
        public DemoPoint goal;
        // A draft room may have no spawn yet; such a room is refused before it can be played or exported.
        public bool noSpawn;
        public DemoSpike[] spikes;
        public DemoLaser[] lasers;
        public DemoCrystal[] crystals;
        [NonSerialized] public DemoPlatform[] platforms;
        // Custom maps may be larger than one screen; 0 keeps the default 320×176 grid.
        public int width, height;
        public int GridWidth => width != 0 ? width : 320;
        public int GridHeight => height != 0 ? height : 176;
        // A room without a stated height is the 320×180 view; a stated one is its own height.
        public int RoomHeight => height != 0 ? height : 180;
        // Every spawn, leftmost first.
        public List<(double x, double y)> Spawns => noSpawn ? new List<(double x, double y)>() : new[] { (spawnX, spawnY) }.Concat(extraSpawns?.Select(p => (p.x, p.y)) ?? Array.Empty<(double, double)>())
            .OrderBy(p => p.Item1).ThenBy(p => p.Item2).ToList();

        public Spike[] CreateSpikes() => spikes?.Select(s => new Spike(s.x,s.y,s.length,s.direction,s.phaseMask)).ToArray() ?? Array.Empty<Spike>();
        public Laser[] CreateLasers() => lasers?.Select(l => new Laser(l.x,l.y,l.direction,l.phaseMask)).ToArray() ?? Array.Empty<Laser>();
        public Crystal[] CreateCrystals() => crystals?.Select(c => new Crystal(c.x,c.y,c.phaseMask)).ToArray() ?? Array.Empty<Crystal>();
        public Platform[] CreatePlatforms() => platforms?.Select(p => p.Create()).ToArray() ?? Array.Empty<Platform>();
        public PhaseController CreateController()
        {
            void Cell(double value) { if (value % 8 != 0) throw new InvalidOperationException("Demo geometry must use whole 8px Grid cells."); }
            foreach (var block in blocks) { Cell(block.x); Cell(block.y); Cell(block.w); Cell(block.h); if (block.w < 8 || block.h < 8) throw new InvalidOperationException("Demo cells require positive sizes."); }
            foreach (var spike in CreateSpikes()) { Cell(spike.x); Cell(spike.y); }
            foreach (var laser in CreateLasers()) { Cell(laser.x); Cell(laser.y); }
            foreach (var crystal in CreateCrystals()) { Cell(crystal.x); Cell(crystal.y); }
            var spawns = Spawns;
            if (spawns.Count == 0) throw new InvalidOperationException("The room has no spawn.");
            var start = spawns[0];
            return new PhaseController(blocks.Select(b => new Block(b.x,b.y,b.w,b.h,b.phase,b.phaseMask) { spiked = b.spiked }), start.x, start.y,
                spikes: CreateSpikes(), lasers: CreateLasers(), crystals: CreateCrystals(), width: GridWidth, height: RoomHeight, platforms: CreatePlatforms())
                { goal = goal == null ? null : new Block(goal.x, goal.y, 8, 8) };
        }
    }
    [Serializable]
    public sealed class DemoPoint
    {
        public double x, y;
    }
    [Serializable]
    public sealed class DemoSpike
    {
        public double x,y,length;
        public SpikeDirection direction;
        public int phaseMask;
    }
    [Serializable]
    public sealed class DemoLaser
    {
        public double x, y;
        public LaserDirection direction;
        public int phaseMask;
    }
    [Serializable]
    public sealed class DemoCrystal
    {
        public double x, y;
        public int phaseMask;
    }
    // Pivots are centre cells in visiting order; the first is the start.
    public sealed class DemoPlatform
    {
        public bool vertical, reverse, spiked;
        public int length = 1, phaseMask = 7;
        public (int x, int y)[] pivots;
        public SpikeDirection[] spikes = Array.Empty<SpikeDirection>();
        public Platform Create() => new Platform(vertical, length, pivots, reverse, phaseMask, spiked, spikes);
    }
}
