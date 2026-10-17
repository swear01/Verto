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
        public Crystal[] CreateCrystals() => crystals?.Select(c => new Crystal(c.x,c.y,c.kind,c.phaseMask)).ToArray() ?? Array.Empty<Crystal>();
        public PhaseController CreateController()
        {
            var gear = CreateGear();
            return new PhaseController(blocks.Select(b => new Block(b.x,b.y,b.w,b.h,b.phase,b.phaseMask)), spawnX, spawnY,
                CreateMover(), gears: gear == null ? null : new[] { gear }, spikes: CreateSpikes(), lasers: CreateLasers(), crystals: CreateCrystals());
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
        public CrystalKind kind;
        public int phaseMask;
    }
}
