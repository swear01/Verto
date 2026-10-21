using System;
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
        public DemoSpike[] spikes;
        public DemoLaser[] lasers;
        public DemoCrystal[] crystals;
        // Custom maps may be larger than one screen; 0 keeps the default 320×176 grid.
        public int width, height;
        public int GridWidth => width != 0 ? width : 320;
        public int GridHeight => height != 0 ? height : 176;
        // A one-screen grid sits in the 320×180 view; a taller one is its own height.
        public int RoomHeight => Math.Max(180, GridHeight);

        public Spike[] CreateSpikes() => spikes?.Select(s => new Spike(s.x,s.y,s.length,s.direction,s.phaseMask)).ToArray() ?? Array.Empty<Spike>();
        public Laser[] CreateLasers() => lasers?.Select(l => new Laser(l.x,l.y,l.direction,l.phaseMask)).ToArray() ?? Array.Empty<Laser>();
        public Crystal[] CreateCrystals() => crystals?.Select(c => new Crystal(c.x,c.y,c.phaseMask)).ToArray() ?? Array.Empty<Crystal>();
        public PhaseController CreateController()
        {
            void Cell(double value) { if (value % 8 != 0) throw new InvalidOperationException("Demo geometry must use whole 8px Grid cells."); }
            foreach (var block in blocks) { Cell(block.x); Cell(block.y); Cell(block.w); Cell(block.h); if (block.w < 8 || block.h < 8) throw new InvalidOperationException("Demo cells require positive sizes."); }
            foreach (var spike in CreateSpikes()) { Cell(spike.x); Cell(spike.y); }
            foreach (var laser in CreateLasers()) { Cell(laser.x); Cell(laser.y); }
            foreach (var crystal in CreateCrystals()) { Cell(crystal.x); Cell(crystal.y); }
            return new PhaseController(blocks.Select(b => new Block(b.x,b.y,b.w,b.h,b.phase,b.phaseMask)), spawnX, spawnY,
                spikes: CreateSpikes(), lasers: CreateLasers(), crystals: CreateCrystals(), width: GridWidth, height: RoomHeight);
        }
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
}
