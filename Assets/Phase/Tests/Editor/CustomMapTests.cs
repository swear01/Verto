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
    // The version 1 file for a room: rectangles, pixel positions and numeric directions.
    static JObject V1(PhaseDemoRoom m)
    {
        var data=new JObject{["name"]=m.name,["blocks"]=new JArray(m.blocks.Select(b=>new JObject{["x"]=b.x,["y"]=b.y,["w"]=b.w,["h"]=b.h,["phase"]=b.phase,["phaseMask"]=b.phaseMask})),
            ["spikes"]=new JArray((m.spikes??new DemoSpike[0]).Select(s=>new JObject{["x"]=s.x,["y"]=s.y,["length"]=s.length,["direction"]=(int)s.direction,["phaseMask"]=s.phaseMask}))};
        if(m.lasers!=null)data["lasers"]=new JArray(m.lasers.Select(l=>new JObject{["x"]=l.x,["y"]=l.y,["direction"]=(int)l.direction,["phaseMask"]=l.phaseMask}));
        if(m.crystals!=null)data["crystals"]=new JArray(m.crystals.Select(c=>new JObject{["x"]=c.x,["y"]=c.y,["phaseMask"]=c.phaseMask}));
        data["spawnX"]=m.spawnX;data["spawnY"]=m.spawnY;return data;
    }
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
        Assert.That(map.crystals,Is.Null);Assert.That(map.lasers,Is.Null);
        Assert.That(map.blocks.Length,Is.EqualTo(40),"a 320x8 floor becomes 40 cells");Assert.That(map.spikes.Length,Is.EqualTo(1));
        foreach(var mutate in new Action<JObject>[] {
            d=>d["crystals"]=new JObject(),d=>d["lasers"]=new JArray(new JObject{["x"]=80,["y"]=80,["direction"]=0,["phaseMask"]=7,["length"]=8}),
            d=>d["crystals"]=new JArray(new JObject{["x"]=80,["y"]=80,["kind"]="0",["phaseMask"]=7}),
            d=>d["crystals"]=new JArray(new JObject{["x"]=80,["y"]=80,["kind"]=1,["phaseMask"]=7}),
            d=>d["gears"]=new JObject(),d=>d["platforms"]=new JArray(new JObject{["w"]=24,["h"]=8,["nodeX"]=new JArray(80,96),["nodeY"]=new JArray(80,"80")}),
            d=>d["platforms"]=new JArray(new JObject{["w"]=24,["h"]=8,["nodeX"]=new JArray(80,96),["nodeY"]=new JArray(80,80),["returns"]="true"}),
            d=>d["gears"]=new JArray(new JObject{["wall"]=new JObject{["x"]=72,["y"]=104,["w"]=8},["platform"]=new JObject(),["top"]=80,["endX"]=112}),
            d=>d["gearWall"]=new JObject(),d=>d["nodeX"]=new JArray(),
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
            m=>m.crystals=new[]{new DemoCrystal{x=80,y=160,phaseMask=7}},
            m=>m.lasers=new[]{new DemoLaser{x=4,y=8,direction=LaserDirection.Up,phaseMask=7}},
            m=>m.crystals=new DemoCrystal[880]
        }) {
            var map=Simple();mutate(map);Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Validate(map));
        }
    }
    [Test]
    public void UnsafeSpawnUnsupportedMechanismsAndInvalidMasksAreRejected()
    {
        var shielded=Simple();shielded.lasers=new[]{new DemoLaser{x=0,y=152,direction=LaserDirection.Right,phaseMask=7}};
        Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Validate(shielded),"the beam reaches the spawn");
        shielded.platforms=new[]{new DemoPlatform{pivots=new[]{(2,19),(2,15)}}};
        Assert.DoesNotThrow(()=>PhaseCustomMap.Validate(shielded),"a platform that starts active shields the spawn");
        foreach(var mutate in new Action<PhaseDemoRoom>[] {
            m=>m.spawnX=double.NaN,m=>m.spawnY=double.NegativeInfinity,m=>m.spawnX=320,
            m=>m.spawnY=168,m=>m.spawnY=151,
            m=>m.blocks=new[]{new Block(24,152,8,8,phaseMask:1)},
            m=>m.spikes=new[]{new DemoSpike{x=24,y=152,length=8,direction=SpikeDirection.Down,phaseMask=1}},
            m=>m.blocks[0].phaseMask=0,m=>m.blocks[0].phaseMask=8,m=>m.blocks[0].phase=3,
            m=>m.spikes[0].phaseMask=0,m=>m.spikes[0].direction=(SpikeDirection)4,
            m=>m.crystals=new[]{new DemoCrystal{x=80,y=80,phaseMask=0}},
            m=>m.lasers=new[]{new DemoLaser{x=80,y=8,direction=(LaserDirection)4,phaseMask=7}},
            m=>m.lasers=new[]{new DemoLaser{x=80,y=8,direction=LaserDirection.Down,phaseMask=8}},
            m=>m.lasers=new DemoLaser[]{null},m=>m.crystals=new DemoCrystal[]{null},
            m=>m.lasers=new[]{new DemoLaser{x=24,y=0,direction=LaserDirection.Down,phaseMask=1}},
            m=>m.lasers=new[]{new DemoLaser{x=56,y=160,direction=LaserDirection.Left,phaseMask=5}}
        }) {
            var map=Simple();mutate(map);Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Validate(map));
        }
    }
    [Test]
    public void LasersAndCrystalsRoundTripAndPlayFromTheirOwnCells()
    {
        var map=Simple();
        Assert.That(JObject.Parse(PhaseCustomMap.ToJson(map)).Properties().Select(p=>p.Name),Is.EqualTo(new[]{"format","version","generator","name","size","spawn","terrain","objects"}),"saving always writes the latest format");
        map.blocks=new[]{map.blocks[0],new Block(24,80,8,8,phaseMask:7)};
        map.lasers=new[]{
            new DemoLaser{x=24,y=0,direction=LaserDirection.Down,phaseMask=1},
            new DemoLaser{x=0,y=152,direction=LaserDirection.Right,phaseMask=2},
            new DemoLaser{x=160,y=160,direction=LaserDirection.Up,phaseMask=7}};
        map.crystals=new[]{new DemoCrystal{x=24,y=152,phaseMask=1},new DemoCrystal{x=96,y=128,phaseMask=6}};
        PhaseCustomMap.Validate(map);
        string json=PhaseCustomMap.ToJson(map);var parsed=PhaseCustomMap.Parse(json);
        Assert.That(PhaseCustomMap.ToJson(parsed),Is.EqualTo(json));
        Assert.That(parsed.lasers.Select(l=>(l.x,l.y,l.direction,l.phaseMask)),Is.EqualTo(map.lasers.Select(l=>(l.x,l.y,l.direction,l.phaseMask))));
        Assert.That(parsed.crystals.Select(c=>(c.x,c.y,c.phaseMask)),Is.EqualTo(map.crystals.Select(c=>(c.x,c.y,c.phaseMask))));
        var legacy=V1(map);legacy["crystals"][0]["kind"]=0;
        Assert.That(PhaseCustomMap.ToJson(PhaseCustomMap.Parse(legacy.ToString())),Is.EqualTo(json),"older maps that tag jump crystals with kind 0 still load");
        var controller=parsed.CreateController();
        Assert.That((controller.lasers.Count,controller.crystals.Count),Is.EqualTo((3,2)));
        Assert.That(controller.LaserBeam(controller.lasers[0]).h,Is.EqualTo(72),"terrain stops the beam above the spawn");
        controller.Step(new Controls());
        Assert.That(controller.crystals[0].collected,Is.True,"the spawn crystal is collected at once");Assert.That(controller.deaths,Is.Zero);
    }
    [Test]
    public void LargeRoomsRoundTripTheirSizeAndBoundObjectsByIt()
    {
        var map=Simple();
        Assert.That(JObject.Parse(PhaseCustomMap.ToJson(map))["size"].ToObject<int[]>(),Is.EqualTo(new[]{40,22}),"a one-screen map is 40x22 cells");
        Assert.That(map.CreateController().Height,Is.EqualTo(180),"a one-screen room keeps the 320x180 view");
        map.width=640;map.height=360;
        map.blocks=new[]{new Block(0,168,320,8,phaseMask:7),new Block(600,344,40,8,phaseMask:7),new Block(320,0,8,8,phaseMask:2)};
        map.crystals=new[]{new DemoCrystal{x=624,y=336,phaseMask=7}};
        PhaseCustomMap.Validate(map);
        string json=PhaseCustomMap.ToJson(map);var data=JObject.Parse(json);
        Assert.That(((int)data["size"][0],(int)data["size"][1]),Is.EqualTo((80,45)));
        var parsed=PhaseCustomMap.Parse(json);
        Assert.That((parsed.GridWidth,parsed.GridHeight),Is.EqualTo((640,360)));
        Assert.That(PhaseCustomMap.ToJson(parsed),Is.EqualTo(json));
        var controller=parsed.CreateController();
        Assert.That((controller.Width,controller.Height),Is.EqualTo((640,360)));
        Assert.That(controller.blocks.Count,Is.EqualTo(46),"40 floor cells, 5 more and one blue cell");
        var wide=JObject.Parse(PhaseCustomMap.ToJson(Simple()));wide["size"]=new JArray(80,22);
        Assert.That((PhaseCustomMap.Parse(wide.ToString()).GridWidth,PhaseCustomMap.Parse(wide.ToString()).GridHeight),Is.EqualTo((640,176)));
        Assert.That(Simple().CreateController().Width,Is.EqualTo(320));
        foreach(var mutate in new Action<PhaseDemoRoom>[] {
            m=>m.width=316,m=>m.width=312,m=>m.width=1288,m=>m.height=168,m=>m.height=728,m=>m.height=-8,
            m=>m.blocks[1].x=608,m=>m.blocks[1].y=360,m=>m.spawnX=640,m=>m.spawnY=352,
            m=>m.crystals[0].x=640
        }) {
            var bad=Simple();bad.width=640;bad.height=360;bad.blocks=map.blocks.Select(b=>new Block(b.x,b.y,b.w,b.h,phaseMask:b.phaseMask)).ToArray();
            bad.crystals=new[]{new DemoCrystal{x=624,y=336,phaseMask=7}};
            mutate(bad);Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Validate(bad));
        }
        foreach(var size in new JToken[]{new JArray(39,45),new JArray(161,45),new JArray(80,21),new JArray(80,91),new JArray("80",45),new JArray(80.5,45),new JArray(0,45),new JArray(80)})
        {
            var bad=JObject.Parse(json);bad["size"]=size;
            Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Parse(bad.ToString()),size.ToString());
        }
    }
    [Test]
    public void VersionOneMapsMigrateAndTheLatestVersionIsAlwaysSaved()
    {
        var map=Simple();map.blocks=new[]{map.blocks[0],new Block(24,80,16,16,phaseMask:2)};
        map.spikes=new[]{new DemoSpike{x=80,y=144,length=24,direction=SpikeDirection.Right,phaseMask=3}};
        var old=V1(map);Assert.That(old.ContainsKey("version"),Is.False,"files without a version are version 1");
        var migrated=PhaseCustomMap.Parse(old.ToString());
        var data=JObject.Parse(PhaseCustomMap.ToJson(migrated));
        Assert.That(((int)data["version"],(string)data["format"],(string)data["generator"]),Is.EqualTo((PhaseCustomMap.Version,"verto-map",PhaseCustomMap.Generator)));
        Assert.That(migrated.blocks.Length,Is.EqualTo(44),"40 floor cells and a 2x2 block");
        var terrain=data["terrain"].ToObject<string[]>();
        Assert.That((terrain.Length,terrain[21],terrain[10],terrain[11]),Is.EqualTo((22,new string('7',40),"...22".PadRight(40,'.'),"...22".PadRight(40,'.'))));
        var spikes=data["objects"].Where(o=>(string)o["type"]=="spike").ToList();
        Assert.That(spikes.Select(o=>(o["cell"].ToObject<int[]>().Sum(),(string)o["dir"],(int)o["mask"])),Is.EqualTo(new[]{(28,"right",3),(29,"right",3),(30,"right",3)}.AsEnumerable()),"a 3-cell spike becomes three spike objects");
        Assert.That(PhaseCustomMap.Parse(PhaseCustomMap.ToJson(migrated)).blocks.Length,Is.EqualTo(44));
        Assert.That(PhaseCustomMap.ToJson(PhaseCustomMap.Parse(PhaseCustomMap.ToJson(migrated))),Is.EqualTo(PhaseCustomMap.ToJson(migrated)),"the latest version round-trips");
        var overlap=V1(map);overlap["blocks"][1]["x"]=0;overlap["blocks"][1]["y"]=160;
        Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Parse(overlap.ToString()),"a version 1 map with overlapping cells is still refused");
    }
    [Test]
    public void NewerVersionsAndForeignFilesAreRefusedWithAClearError()
    {
        var data=JObject.Parse(PhaseCustomMap.ToJson(Simple()));
        data["version"]=PhaseCustomMap.Version+1;data["future"]=true;
        var error=Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Parse(data.ToString()));
        Assert.That(error.Message,Does.Contain("第"+(PhaseCustomMap.Version+1)+"版").And.Contain("更新"),"names the version and asks to update the game");
        foreach(var mutate in new Action<JObject>[]{d=>d["format"]="other",d=>d.Remove("format"),d=>d["version"]=0,d=>d["version"]="2",d=>d["version"]=1.5,d=>d.Remove("version"),d=>d["extra"]=1})
        {
            var bad=JObject.Parse(PhaseCustomMap.ToJson(Simple()));mutate(bad);
            Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Parse(bad.ToString()));
        }
    }
    [Test]
    public void PlatformsAndSpikeCoveredTerrainRoundTrip()
    {
        var map=Simple();
        map.blocks=new[]{map.blocks[0],new Block(24,80,8,8,-1,2){spiked=true}};
        map.platforms=new[]{new DemoPlatform{length=3,pivots=new[]{(10,12),(14,12),(14,6)},phaseMask=5,spiked=false,reverse=true,spikes=new[]{SpikeDirection.Up,SpikeDirection.Left}}};
        PhaseCustomMap.Validate(map);
        string json=PhaseCustomMap.ToJson(map);var data=JObject.Parse(json);
        Assert.That(data["terrain"][10].ToString(),Does.StartWith("...B"),"B is spike-covered terrain in color 2");
        var platform=data["objects"].Single(o=>(string)o["type"]=="platform");
        Assert.That(((string)platform["orientation"],(int)platform["length"],(bool)platform["reverse"],(int)platform["mask"],platform["pivots"].ToString(Formatting.None),platform["spikes"].ToString(Formatting.None)),
            Is.EqualTo(("horizontal",3,true,5,"[[10,12],[14,12],[14,6]]","[\"up\",\"left\"]")));
        var parsed=PhaseCustomMap.Parse(json);
        Assert.That(PhaseCustomMap.ToJson(parsed),Is.EqualTo(json));
        Assert.That(parsed.blocks.Count(b=>b.spiked),Is.EqualTo(1));
        var controller=parsed.CreateController();
        Assert.That((controller.platforms.Count,controller.spikes.Count,controller.blocks.Count(b=>b.spiked)),Is.EqualTo((1,3,1)),"two attached rows join the plain spike");
        Assert.That((controller.platforms[0].x,controller.platforms[0].y,controller.platforms[0].reverse),Is.EqualTo((72d,96d,true)));
        foreach(var mutate in new Action<JObject>[]{
            d=>d["objects"][0]["length"]=2,d=>d["objects"][0]["orientation"]="diagonal",d=>d["objects"][0]["pivots"]=new JArray(new JArray(10,12)),
            d=>d["objects"][0]["pivots"]=new JArray(new JArray(10,12),new JArray(0,12)),d=>d["objects"][0]["spikes"]=new JArray("sideways"),
            d=>d["objects"][0]["mask"]=0,d=>d["objects"][0]["spiked"]="no",d=>d["objects"][0]["colour"]=1,d=>d["terrain"][0]="8",d=>d["terrain"][0]="H",
            d=>d["objects"]=new JArray(new JObject{["type"]="dash"})
        })
        {
            var bad=JObject.Parse(json);bad["objects"]=new JArray(data["objects"].Where(o=>(string)o["type"]=="platform"));mutate(bad);
            Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Parse(bad.ToString()));
        }
        map.platforms[0].pivots=new[]{(5,21),(5,12)};map.platforms[0].length=1;
        Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Validate(map),"a platform may not start on another object");
        map.platforms[0].pivots=new[]{(3,19),(5,12)};
        Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Validate(map),"nor on the spawn");
        map.platforms[0].pivots=new[]{(10,12),(14,12)};map.platforms[0].spikes=new SpikeDirection[0];
        PhaseCustomMap.Validate(map);
        map.platforms=new[]{map.platforms[0],new DemoPlatform{length=1,pivots=new[]{(10,12),(10,5)},spikes=new SpikeDirection[0]}};
        Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Validate(map),"two platforms may not share a starting cell");
    }
    [Test]
    public void VerticalPlatformsRoundTripAndMalformedPlatformsAndHugeNumbersAreRefused()
    {
        var map=Simple();
        map.platforms=new[]{new DemoPlatform{vertical=true,length=3,pivots=new[]{(5,10),(5,6)},spikes=new[]{SpikeDirection.Left}}};
        PhaseCustomMap.Validate(map);string json=PhaseCustomMap.ToJson(map);
        Assert.That((string)JObject.Parse(json)["objects"].Single(o=>(string)o["type"]=="platform")["orientation"],Is.EqualTo("vertical"));
        var parsed=PhaseCustomMap.Parse(json);
        Assert.That(PhaseCustomMap.ToJson(parsed),Is.EqualTo(json));
        Assert.That((parsed.platforms[0].vertical,parsed.CreateController().platforms[0].h),Is.EqualTo((true,24d)));
        foreach(var mutate in new Action<DemoPlatform>[]{
            p=>p.pivots=null,p=>p.pivots=new[]{(5,10)},p=>p.length=2,p=>p.length=0,p=>p.phaseMask=0,p=>p.phaseMask=8,
            p=>p.spikes=new[]{(SpikeDirection)9},p=>p.spikes=null,p=>p.pivots=new[]{(5,10),(5,-3)},p=>p.pivots=new[]{(5,10),(500,6)}})
        {
            var bad=Simple();bad.platforms=new[]{new DemoPlatform{vertical=true,length=3,pivots=new[]{(5,10),(5,6)},spikes=new SpikeDirection[0]}};
            mutate(bad.platforms[0]);
            Assert.Throws<ArgumentException>(()=>PhaseCustomMap.ToJson(bad),"writing checks like validating");
            Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Validate(bad));
        }
        Assert.Throws<ArgumentException>(()=>PhaseCustomMap.ToJson(new PhaseDemoRoom{blocks=Simple().blocks,platforms=new DemoPlatform[]{null}}));
        var rows=Simple();rows.platforms=new[]{new DemoPlatform{length=3,pivots=new[]{(5,10),(5,0)},spikes=new[]{SpikeDirection.Up}}};
        Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Validate(rows),"the spike row would leave the room at the second stop");
        Assert.Throws<ArgumentException>(()=>PhaseCustomMap.ToJson(rows));
        rows.platforms[0].pivots=new[]{(5,10),(5,1)};PhaseCustomMap.Validate(rows);
        var huge=JObject.Parse(json);huge["spawn"]=new JArray(1e18,19);
        Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Parse(huge.ToString()),"a huge number cannot wrap past the room checks");
        var old=V1(Simple());old["spawnX"]=1e18;
        Assert.Throws<ArgumentException>(()=>PhaseCustomMap.Parse(old.ToString()));
    }
}
