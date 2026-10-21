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
        public double[] nodeX, nodeY;
        public double moverW = 24, moverH = 8;
        public bool returns;
        public Block gearWall, gearPlatform;
        public bool hasGear;
        public double gearTop, gearEndX;
        public DemoSpike[] spikes;
        public DemoLaser[] lasers;
        public DemoCrystal[] crystals;
        // Custom maps hold any number of platforms and gears; built-in rooms keep the single mover and gear above.
        public DemoPlatform[] platforms;
        public DemoGear[] gears;
        // Custom maps may be larger than one screen; 0 keeps the default 320×176 grid.
        public int width, height;
        public int GridWidth => width != 0 ? width : 320;
        public int GridHeight => height != 0 ? height : 176;
        // A one-screen grid sits in the 320×180 view; a taller one is its own height.
        public int RoomHeight => Math.Max(180, GridHeight);

        public MovingPlatform CreateMover()
        {
            if (nodeX == null || nodeX.Length == 0) return null;
            if (nodeY == null || nodeX.Length < 2 || nodeY.Length != nodeX.Length || moverW <= 0 || moverH <= 0)
                throw new InvalidOperationException("Demo mover requires matching nodes and positive size.");
            return new MovingPlatform(nodeX.Select((x,i) => new[] { x,nodeY[i] }).ToArray(), moverW, moverH, returns);
        }
        public ClimbGear CreateGear() => hasGear ? new ClimbGear(gearWall, gearTop, gearPlatform, gearEndX) : null;
        public Spike[] CreateSpikes() => spikes?.Select(s => new Spike(s.x,s.y,s.length,s.direction,s.phaseMask)).ToArray() ?? Array.Empty<Spike>();
        public Laser[] CreateLasers() => lasers?.Select(l => new Laser(l.x,l.y,l.direction,l.phaseMask)).ToArray() ?? Array.Empty<Laser>();
        public MovingPlatform[] CreatePlatforms() => platforms?.Select(p => p.Create()).ToArray() ?? Array.Empty<MovingPlatform>();
        public ClimbGear[] CreateGears() => gears?.Select(g => g.Create()).ToArray() ?? Array.Empty<ClimbGear>();
        public Crystal[] CreateCrystals() => crystals?.Select(c => new Crystal(c.x,c.y,c.kind,c.phaseMask)).ToArray() ?? Array.Empty<Crystal>();
        public PhaseController CreateController()
        {
            void Cell(double value) { if (value % 8 != 0) throw new InvalidOperationException("Demo geometry must use whole 8px Grid cells."); }
            foreach (var block in blocks) { Cell(block.x); Cell(block.y); Cell(block.w); Cell(block.h); if (block.w < 8 || block.h < 8) throw new InvalidOperationException("Demo cells require positive sizes."); }
            foreach (var spike in CreateSpikes()) { Cell(spike.x); Cell(spike.y); }
            foreach (var laser in CreateLasers()) { Cell(laser.x); Cell(laser.y); }
            foreach (var crystal in CreateCrystals()) { Cell(crystal.x); Cell(crystal.y); }
            if (nodeX != null) { foreach (var x in nodeX) Cell(x); if (nodeY != null) foreach (var y in nodeY) Cell(y); Cell(moverW); Cell(moverH); }
            if (hasGear) { foreach (var b in new[] { gearWall, gearPlatform }) { Cell(b.x); Cell(b.y); Cell(b.w); Cell(b.h); } Cell(gearTop); Cell(gearEndX); }
            var gear = CreateGear(); var mover = CreateMover();
            return new PhaseController(blocks.Select(b => new Block(b.x,b.y,b.w,b.h,b.phase,b.phaseMask)), spawnX, spawnY,
                (mover == null ? CreatePlatforms() : CreatePlatforms().Prepend(mover)), gears: gear == null ? CreateGears() : CreateGears().Prepend(gear),
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
    public sealed class DemoPlatform
    {
        public double w = 24, h = 8;
        public double[] nodeX, nodeY;
        public bool returns;
        public int phaseMask;
        public MovingPlatform Create()
        {
            if (nodeX == null || nodeY == null || nodeX.Length != nodeY.Length) throw new ArgumentException("Platform requires matching nodes.");
            return new MovingPlatform(nodeX.Select((x,i) => new[] { x,nodeY[i] }).ToArray(), w, h, returns, phaseMask: phaseMask);
        }
    }
    [Serializable]
    public sealed class DemoGear
    {
        public Block wall, platform;
        public double top, endX;
        public int phaseMask;
        public ClimbGear Create()
        {
            if (wall == null || platform == null) throw new ArgumentException("Gear requires a wall and a platform.");
            return new ClimbGear(new Block(wall.x,wall.y,wall.w,wall.h,phaseMask:phaseMask), top, new Block(platform.x,platform.y,platform.w,platform.h,phaseMask:phaseMask), endX);
        }
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
        public CrystalKind kind;
        public int phaseMask;
    }
}
