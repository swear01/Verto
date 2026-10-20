using System;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Phase;

public class CustomMapTests
{
    static PhaseDemoRoom Simple() => new PhaseDemoRoom {
        name="Custom",spawnX=24,spawnY=152,
        blocks=new[]{new Block(0,168,320,8,phaseMask:7)},
        spikes=new[]{new DemoSpike{x=80,y=160,length=8,direction=SpikeDirection.Up,phaseMask=7}}
    };
    [Test]
    public void JsonBoundaryUsesOnlySupportedFieldsAndPreservesNullMechanisms()
    {
        var source=Simple();
        var data=new JObject {
            ["name"]=source.name,["spawnX"]=source.spawnX,["spawnY"]=source.spawnY,
            ["blocks"]=JArray.FromObject(source.blocks),["spikes"]=JArray.FromObject(source.spikes)
        };
        // Block's computed Mask is not part of the wire schema.
        foreach(JObject block in data["blocks"])block.Remove("Mask");
        string json=data.ToString(Formatting.None);var map=PhaseCustomMap.Parse(json);
        Assert.That(map.crystals,Is.Null);Assert.That(map.lasers,Is.Null);Assert.That(map.gearWall,Is.Null);Assert.That(map.gearPlatform,Is.Null);Assert.That(map.CreateMover(),Is.Null);
        Assert.That(map.blocks.Length,Is.EqualTo(1));Assert.That(map.spikes.Length,Is.EqualTo(1));
        foreach(var mutate in new Action<JObject>[] {
            d=>d["crystals"]=new JObject(),d=>d["lasers"]=new JArray(new JObject{["x"]=80,["y"]=80,["direction"]=0,["phaseMask"]=7,["length"]=8}),
            d=>d["crystals"]=new JArray(new JObject{["x"]=80,["y"]=80,["kind"]="0",["phaseMask"]=7}),
            d=>d["gears"]=new JArray(),d=>d["gearWall"]=new JObject(),d=>d["nodeX"]=new JArray(),
            d=>d["spawnX"]="24",d=>d.Remove("spawnY"),d=>d["spawnX"]=double.NaN,
            d=>d["name"]=new string('x',65),d=>d["blocks"][0]["unknown"]=1,
            d=>d["blocks"][0]["phaseMask"]=1.5,d=>((JObject)d["spikes"][0]).Remove("direction")
        }) {
            var bad=(JObject)data.DeepClone();mutate(bad);
            Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Parse(bad.ToString(Formatting.None)));
        }
        Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Parse(json.Replace("\"spawnX\":24", "\"spawnX\":24,\"spawnX\":32")));
        Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Parse(new string(' ',PhaseCustomMap.MaxJsonLength+1)));
        Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Parse("[]"));
    }
    [Test]
    public void SafeMapMovesJumpsAndRespawnsAtItsOwnCheckpoint()
    {
        var map=Simple();PhaseCustomMap.Validate(map);var controller=map.CreateController();
        for(int i=0;i<30;i++)controller.Step(new Controls());
        Assert.That(controller.player.grounded,Is.True);
        controller.Step(new Controls{jump=true,jumpHeld=true});Assert.That(controller.phase,Is.EqualTo(1));
        double x=controller.player.x;for(int i=0;i<5;i++)controller.Step(new Controls{x=1});
        Assert.That(controller.player.x,Is.GreaterThan(x));
        controller.player.y=200;controller.Step(new Controls());Assert.That(controller.deaths,Is.EqualTo(1));
        for(int i=0;i<20;i++)controller.Step(new Controls());
        Assert.That(controller.phase,Is.Zero);Assert.That(controller.player.x,Is.EqualTo(map.spawnX));
        Assert.That(controller.respawn,Is.Zero);Assert.That(controller.player.y,Is.LessThan(168));
        map.blocks=new[]{new Block(24,152,8,8,phaseMask:2)};map.spikes=null;
        PhaseCustomMap.Validate(map);Assert.That(map.CreateController().Solid(),Is.Empty,"blue-only terrain may overlap a red spawn");
    }
    [Test]
    public void InvalidGeometryAndOverlappingCellsAreRejectedBeforePlaying()
    {
        Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Validate(null));
        foreach(var mutate in new Action<PhaseDemoRoom>[] {
            m=>m.blocks=null,m=>m.blocks=Array.Empty<Block>(),m=>m.blocks=new Block[]{null},
            m=>m.blocks[0].x=1,m=>m.blocks[0].y=-8,m=>m.blocks[0].w=328,m=>m.blocks[0].h=0,
            m=>m.blocks[0].x=double.NaN,m=>m.blocks[0].h=double.PositiveInfinity,
            m=>m.blocks=new[]{m.blocks[0],new Block(0,168,8,8,phaseMask:2)},
            m=>m.spikes[0].y=168,m=>m.spikes[0].x=double.NaN,m=>m.spikes[0].length=16*320,
            m=>m.spikes[0].length=double.PositiveInfinity,m=>m.spikes[0].length=9,
            m=>m.spikes=new[]{m.spikes[0],m.spikes[0]},m=>m.blocks=new Block[881],
            m=>m.lasers=new[]{new DemoLaser{x=0,y=168,direction=LaserDirection.Up,phaseMask=7}},
            m=>m.crystals=new[]{new DemoCrystal{x=80,y=160,kind=CrystalKind.Dash,phaseMask=7}},
            m=>m.lasers=new[]{new DemoLaser{x=4,y=8,direction=LaserDirection.Up,phaseMask=7}},
            m=>m.crystals=new DemoCrystal[880]
        }) {
            var map=Simple();mutate(map);Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Validate(map));
        }
    }
    [Test]
    public void UnsafeSpawnUnsupportedMechanismsAndInvalidMasksAreRejected()
    {
        foreach(var mutate in new Action<PhaseDemoRoom>[] {
            m=>m.spawnX=double.NaN,m=>m.spawnY=double.NegativeInfinity,m=>m.spawnX=320,
            m=>m.spawnY=168,m=>m.spawnY=151,
            m=>m.blocks=new[]{new Block(24,152,8,8,phaseMask:1)},
            m=>m.spikes=new[]{new DemoSpike{x=24,y=152,length=8,direction=SpikeDirection.Down,phaseMask=1}},
            m=>m.blocks[0].phaseMask=0,m=>m.blocks[0].phaseMask=8,m=>m.blocks[0].phase=3,
            m=>m.spikes[0].phaseMask=0,m=>m.spikes[0].direction=(SpikeDirection)4,
            m=>m.crystals=new[]{new DemoCrystal{x=80,y=80,kind=(CrystalKind)2,phaseMask=7}},
            m=>m.crystals=new[]{new DemoCrystal{x=80,y=80,kind=CrystalKind.Jump,phaseMask=0}},
            m=>m.lasers=new[]{new DemoLaser{x=80,y=8,direction=(LaserDirection)4,phaseMask=7}},
            m=>m.lasers=new[]{new DemoLaser{x=80,y=8,direction=LaserDirection.Down,phaseMask=8}},
            m=>m.lasers=new DemoLaser[]{null},m=>m.crystals=new DemoCrystal[]{null},
            m=>m.lasers=new[]{new DemoLaser{x=24,y=0,direction=LaserDirection.Down,phaseMask=1}},
            m=>m.lasers=new[]{new DemoLaser{x=56,y=160,direction=LaserDirection.Left,phaseMask=5}},
            m=>m.nodeX=new[]{0.0},m=>m.nodeY=new[]{0.0},m=>m.hasGear=true,
            m=>m.gearWall=new Block(0,0,8,8),m=>m.gearPlatform=new Block(0,0,8,8),
            m=>m.gearTop=8,m=>m.gearEndX=8,m=>m.returns=true,m=>m.moverW=double.NaN
        }) {
            var map=Simple();mutate(map);Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Validate(map));
        }
    }
    [Test]
    public void LasersAndCrystalsRoundTripAndPlayFromTheirOwnCells()
    {
        var map=Simple();
        Assert.That(JObject.Parse(PhaseCustomMap.ToJson(map)).Properties().Select(p=>p.Name),Is.EqualTo(new[]{"name","blocks","spikes","spawnX","spawnY"}),"maps without lasers or crystals keep five fields");
        map.blocks=new[]{map.blocks[0],new Block(24,80,8,8,phaseMask:7)};
        map.lasers=new[]{
            new DemoLaser{x=24,y=0,direction=LaserDirection.Down,phaseMask=1},
            new DemoLaser{x=0,y=152,direction=LaserDirection.Right,phaseMask=2},
            new DemoLaser{x=160,y=160,direction=LaserDirection.Up,phaseMask=7}};
        map.crystals=new[]{new DemoCrystal{x=24,y=152,kind=CrystalKind.Dash,phaseMask=1},new DemoCrystal{x=96,y=128,kind=CrystalKind.Jump,phaseMask=6}};
        PhaseCustomMap.Validate(map);
        string json=PhaseCustomMap.ToJson(map);var parsed=PhaseCustomMap.Parse(json);
        Assert.That(PhaseCustomMap.ToJson(parsed),Is.EqualTo(json));
        Assert.That(parsed.lasers.Select(l=>(l.x,l.y,l.direction,l.phaseMask)),Is.EqualTo(map.lasers.Select(l=>(l.x,l.y,l.direction,l.phaseMask))));
        Assert.That(parsed.crystals.Select(c=>(c.x,c.y,c.kind,c.phaseMask)),Is.EqualTo(map.crystals.Select(c=>(c.x,c.y,c.kind,c.phaseMask))));
        var controller=parsed.CreateController();
        Assert.That((controller.lasers.Count,controller.crystals.Count),Is.EqualTo((3,2)));
        Assert.That(controller.LaserBeam(controller.lasers[0]).h,Is.EqualTo(72),"terrain stops the beam above the spawn");
        controller.Step(new Controls());
        Assert.That(controller.crystals[0].collected,Is.True,"the spawn crystal is collected at once");Assert.That(controller.deaths,Is.Zero);
    }
}
