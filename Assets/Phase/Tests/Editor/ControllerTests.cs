using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Newtonsoft.Json;
using Phase;

public class ControllerTests
{
    [Serializable] public class XY { public double x, y; }
    [Serializable] public class Initial { public XY pos, speed; public bool on_ground; public bool facing = true; public double max_fall = 160, stamina = 110; }
    [Serializable] public class Solid { public double x, y, width, height; }
    [Serializable] public class Input { public bool up, down, left, right, jump, dash, grab; }
    [Serializable] public class Expected { public double x, y, vx, vy, stamina; }
    [Serializable] public class Case { public string name; public Initial initial; public Solid floor; public Solid[] solids; public Input[] inputs; public Expected[] expected; }
    [Serializable] public class Fixture { public double dt; public Case[] cases; }

    [TestCase("movement")]
    [TestCase("actions")]
    [TestCase("climb")]
    public void ImmutableReferenceTraces(string name)
    {
        var f = JsonConvert.DeserializeObject<Fixture>(File.ReadAllText("Assets/Phase/Tests/Fixtures/reference-" + name + ".json"));
        foreach (var c in f.cases)
        {
            var blocks = new List<Block>();
            foreach (var b in c.solids ?? new[] { c.floor }) for (int phase = 0; phase < 3; phase++) blocks.Add(new Block(b.x, b.y, b.width, b.height, phase));
            var init = c.initial; var s = new PhaseController(blocks, init.pos.x - 4, init.pos.y - 11);
            var p = s.player; p.vx = init.speed.x; p.vy = init.speed.y; p.grounded = init.on_ground;
            p.coyote = p.grounded ? .1 : 0; p.facing = init.facing ? 1 : -1; p.maxFall = init.max_fall; p.stamina = init.stamina;
            var previous = new Input();
            for (int i = 0; i < c.inputs.Length; i++)
            {
                var a = c.inputs[i]; s.Step(new Controls { x = name == "movement" ? 1 : (a.right ? 1 : 0) - (a.left ? 1 : 0), y = (a.down ? 1 : 0) - (a.up ? 1 : 0), jump = a.jump && !previous.jump, jumpHeld = a.jump, dash = a.dash && !previous.dash, dashHeld = a.dash, grab = a.grab }, f.dt);
                previous = a; var e = c.expected[i];
                if (c.name == "climb_to_exhaustion" && i >= 133)
                {
                    int[] slowY = {143,143,144,145,145,146,147,148,148,149,150,151,151,152,153,154,154};
                    e = new Expected { x=e.x,y=slowY[i-133],vx=e.vx,vy=Math.Min(e.vy,45),stamina=e.stamina };
                }
                string frame = c.name + " frame " + (i + 1);
                Assert.That(p.x + 4, Is.EqualTo(e.x), frame + " x"); Assert.That(p.y + p.h, Is.EqualTo(e.y), frame + " y");
                Assert.That(p.vx, Is.EqualTo(e.vx).Within(.002), frame + " vx"); Assert.That(p.vy, Is.EqualTo(e.vy).Within(.002), frame + " vy");
                if (name != "movement") Assert.That(p.stamina, Is.EqualTo(e.stamina).Within(.002), frame + " stamina");
            }
        }
    }
    [Test]
    public void PhaseMasksKeepLegacyAndAllSevenCombinations()
    {
        foreach(int legacy in new[]{-1,0,1,2})foreach(int phase in new[]{0,1,2})
            Assert.That(new Block(0,0,8,8,legacy).ActiveInPhase(phase),Is.EqualTo(legacy<0||legacy==phase));
        for(int mask=1;mask<=7;mask++)for(int phase=0;phase<3;phase++)
        {
            var block=new Block(0,160,320,24,phaseMask:mask);var room=new PhaseController(new[]{block},32,120);room.phase=phase;
            Assert.That(block.Mask,Is.EqualTo(mask));Assert.That(room.Solid().Contains(block),Is.EqualTo((mask&(1<<phase))!=0));
        }
        Assert.That(new Block(0,0,8,8,phase:3,phaseMask:5).Mask,Is.EqualTo(5),"HTML mixed-phase metadata is overridden by its explicit mask");
        Assert.Throws<ArgumentOutOfRangeException>(()=>new Block(0,0,8,8,phaseMask:8));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new Block(0,0,8,8,phaseMask:-1));
    }
    [Test]
    public void MixedMaterializationDefersOnlyNewlySolidBlocks()
    {
        foreach(int mask in new[]{3,5,6,7})
        {
            var b=new Block(120,100,8,16,phaseMask:mask);var room=new PhaseController(new[]{b},120,100);
            room.player.grounded=false;room.player.coyote=.1;
            room.Step(new Controls{jump=true});
            Assert.That(room.blocked.Contains(0),Is.EqualTo((mask&1)==0&&(mask&2)!=0));
            if(room.blocked.Contains(0)) { room.player.x=160;room.Step(new Controls());Assert.That(room.blocked,Is.Empty);Assert.That(room.Solid(),Does.Contain(b)); }
        }
    }
    [Test]
    public void PhaseSpikesUseRootStripsAndSweptCollisionInEveryDirection()
    {
        foreach(SpikeDirection direction in Enum.GetValues(typeof(SpikeDirection)))
        {
            var spike=new Spike(80,80,16,direction,3);var hit=spike.Hitbox;
            Assert.That(direction==SpikeDirection.Up||direction==SpikeDirection.Down?hit.h:hit.w,Is.EqualTo(3));
            for(int phase=0;phase<3;phase++)
            {
                var room=new PhaseController(Array.Empty<Block>(),32,40,spikes:new[]{spike});room.phase=phase;
                room.player.x=hit.x;room.player.y=hit.y;
                Assert.That(room.CheckSpikes(),Is.EqualTo(phase!=2));Assert.That(room.deaths,Is.EqualTo(phase!=2?1:0));
                room.CheckSpikes();Assert.That(room.deaths,Is.LessThanOrEqualTo(1),"a death is counted once");
            }
            var sweep=new PhaseController(Array.Empty<Block>(),32,40,spikes:new[]{spike});
            var from=new Block(hit.x-40,hit.y,8,11);sweep.player.x=hit.x+40;sweep.player.y=hit.y;
            Assert.That(sweep.CheckSpikes(from),Is.True,"a dash cannot tunnel through a thin root");
            var reverse=new PhaseController(Array.Empty<Block>(),32,40,spikes:new[]{spike});from.x=hit.x+40;reverse.player.x=hit.x-40;reverse.player.y=hit.y;
            Assert.That(reverse.CheckSpikes(from),Is.True,"reverse sweep");
            var diagonal=new PhaseController(Array.Empty<Block>(),32,40,spikes:new[]{spike});from=new Block(hit.x-40,hit.y-40,8,11);diagonal.player.x=hit.x+40;diagonal.player.y=hit.y+40;Assert.That(diagonal.CheckSpikes(from),Is.True,"diagonal sweep");
            var tangent=new PhaseController(Array.Empty<Block>(),32,40,spikes:new[]{spike});from.x=hit.x-40;from.y=hit.y-11;tangent.player.x=hit.x+40;tangent.player.y=hit.y-11;
            Assert.That(tangent.CheckSpikes(from),Is.False,"touching the strip boundary is not overlap");
        }
        for(int mask=1;mask<=7;mask++)for(int phase=0;phase<3;phase++)
        { var spike=new Spike(80,80,8,SpikeDirection.Up,mask);Assert.That(spike.ActiveInPhase(phase),Is.EqualTo((mask&(1<<phase))!=0)); }
        Assert.Throws<ArgumentOutOfRangeException>(()=>new Spike(0,0,7,SpikeDirection.Up));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new Spike(0,0,8,(SpikeDirection)4));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new Spike(0,0,8,SpikeDirection.Up,0));
    }
    [Test]
    public void SpikesActivateOnJumpKillPlatformRidersAndReset()
    {
        var blue=new Spike(120,100,8,SpikeDirection.Up,2);
        var room=new PhaseController(Array.Empty<Block>(),32,40,spikes:new[]{blue});room.player.x=120;room.player.y=100;room.player.grounded=false;room.player.coyote=.1;
        room.Step(new Controls{jump=true});Assert.That(room.phase,Is.EqualTo(1));Assert.That(room.deaths,Is.EqualTo(1));Assert.That(room.respawn,Is.GreaterThan(0));
        Ticks(room,20);Assert.That(room.player.x,Is.EqualTo(32));Assert.That(room.phase,Is.Zero);Assert.That(room.player.stamina,Is.EqualTo(110));
        var mover=new MovingPlatform(new[]{new[]{72.0,72},new[]{216.0,72}},48,8);
        var red=new Spike(104,56,16,SpikeDirection.Left,1);
        room=new PhaseController(Array.Empty<Block>(),88,61,mover,spikes:new[]{red});
        room.mover.fromX=72;room.mover.fromY=72;room.mover.index=1;room.mover.duration=.3;room.mover.moving=true;
        for(int i=0;i<30&&room.deaths==0;i++)room.Step(new Controls());
        Assert.That(room.deaths,Is.EqualTo(1),"platform carry checks the complete swept path");
        Ticks(room,20);Assert.That(room.mover.x,Is.EqualTo(72));Assert.That(room.mover.moving,Is.False);
        room=new PhaseController(Array.Empty<Block>(),32,40,spikes:new[]{new Spike(40,40,16,SpikeDirection.Left,1)});
        room.player.grounded=false;room.player.dashTime=.3;room.player.vx=240;room.player.dx=1;
        room.Step(new Controls(),.1);Assert.That(room.deaths,Is.EqualTo(1),"an in-progress dash crossing the complete strip cannot tunnel");

    }
    static PhaseController Arena(double width = 320) => new PhaseController(new[] { new Block(0,150,width,8), new Block(200,33,8,117) }, 67,139);
    static PhaseController GearArena(params Block[] obstacles)
    {
        var blocks = new List<Block> { new Block(0,160,88,24) }; blocks.AddRange(obstacles);
        return new PhaseController(blocks,64,149,gears:new[] {
            new ClimbGear(new Block(72,88,8,72),64,new Block(88,104,48,8),112)
        });
    }
    [Test]
    public void GearRackMustStayInsideItsPlatformAcrossTheWholeTravel()
    {
        Assert.Throws<ArgumentException>(() => new ClimbGear(new Block(72,104,8,56),64,new Block(104,104,32,8),144));
        Assert.DoesNotThrow(() => new ClimbGear(new Block(72,104,8,56),80,new Block(88,80,48,8),112));
    }
    [Test]
    public void GearCarryChecksTheSpikeSweepAfterRelativeClimbing()
    {
        var s=GearArena();var p=s.player;p.y=144;p.grounded=false;p.climbing=true;p.facing=1;p.climbWait=0;
        s.spikes.Add(new Spike(56,135,16,SpikeDirection.Up));
        Ticks(s,4,new Controls { y=-1,grab=true });
        Assert.That(s.deaths,Is.EqualTo(1),"the relative climb only touches the edge; the wall carry crosses the lethal root");
        Ticks(s,24);Assert.That(s.respawn,Is.Zero);Assert.That(s.gears[0].Lift,Is.Zero);
    }
    [Test]
    public void PhaseTerrainWaitsForTheGearToLeaveBeforeMaterializing()
    {
        var s=GearArena(new Block(100,104,8,8,1));var gear=s.gears[0];
        s.Step(new Controls { jump=true,jumpHeld=true });
        Assert.That(s.phase,Is.EqualTo(1));Assert.That(s.blocked.Contains(1),Is.True);
        Ticks(s,4);Assert.That(s.blocked.Contains(1),Is.True,"terrain cannot appear inside the parked platform");
        gear.platform.x=112;Ticks(s,1);Assert.That(s.blocked.Contains(1),Is.False);
    }
    static void Ticks(PhaseController s, int count, Controls input = default)
    { for (int i = 0; i < count; i++) { var a = input; a.jump &= i == 0; a.dash &= i == 0; s.Step(a); } }
    [Test]
    public void GearClimbCarriesPlayerAndDrivesHorizontalPlatformInBothDirections()
    {
        var s=GearArena();var gear=s.gears[0];
        Ticks(s,30,new Controls { x=1,y=-1,grab=true });
        Assert.That(gear.wall.y,Is.LessThan(88));Assert.That(gear.platform.x,Is.GreaterThan(88));
        Assert.That(s.player.y,Is.LessThan(149-gear.Lift),"wall carries the player in addition to relative climbing");
        Assert.That(gear.platform.y,Is.EqualTo(104));Assert.That(s.phase,Is.Zero);
        double wallY=gear.wall.y,platformX=gear.platform.x;
        Ticks(s,12,new Controls { grab=true });
        Assert.That((gear.wall.y,gear.platform.x),Is.EqualTo((wallY,platformX)),"hanging does not drive gears");
        Ticks(s,16,new Controls { y=1,grab=true });
        Assert.That(gear.wall.y,Is.GreaterThan(wallY));Assert.That(gear.platform.x,Is.LessThan(platformX));
        wallY=gear.wall.y;platformX=gear.platform.x;
        Ticks(s,6,new Controls { y=-1 });
        Assert.That((gear.wall.y,gear.platform.x),Is.EqualTo((wallY,platformX)),"input without grabbing does not drive gears");
        s.Reset();Assert.That((s.gears[0].wall.y,s.gears[0].platform.x),Is.EqualTo((88.0,88.0)));
    }
    [Test]
    public void GearEndpointsAndObstructionsStopBothLinkedParts()
    {
        var s=GearArena();
        Ticks(s,90,new Controls { x=1,y=-1,grab=true });
        Assert.That((s.gears[0].wall.y,s.gears[0].platform.x),Is.EqualTo((64.0,112.0)));
        var p=s.player;p.x=64;p.y=72;p.vx=p.vy=0;p.facing=1;p.climbing=true;p.climbWait=0;p.stamina=110;
        Ticks(s,90,new Controls { y=1,grab=true });
        Assert.That((s.gears[0].wall.y,s.gears[0].platform.x),Is.EqualTo((88.0,88.0)));
        s=GearArena(new Block(140,104,8,8));
        Ticks(s,55,new Controls { x=1,y=-1,grab=true });
        Assert.That((s.gears[0].wall.y,s.gears[0].platform.x),Is.EqualTo((84.0,92.0)),"platform obstruction stops the whole drive");
        Assert.That(s.deaths,Is.Zero);Assert.That(PhaseController.Overlap(s.gears[0].platform,s.blocks[1]),Is.False);
        s=GearArena(new Block(56,136,16,8));p=s.player;p.y=144;p.climbing=true;p.facing=1;
        Ticks(s,12,new Controls { y=-1,grab=true });
        Assert.That(s.gears[0].Lift,Is.Zero,"a blocked climb does not drive the mechanism");
    }
    [Test]
    public void AdaptiveRoomExtendsFloorAndEdgesWithoutResettingState()
    {
        var s = new PhaseController(new[] { new Block(0,160,320,24), new Block(240,80,8,80) },32,149);
        var player = s.player; s.phase = 2; s.jumps = 3; player.stamina = 63;
        s.ResizeRoom(427);
        Assert.That(s.blocks[0].w,Is.EqualTo(427)); Assert.That(s.blocks[1].x,Is.EqualTo(240));
        player.x=400; Ticks(s,2); Assert.That(player.grounded,Is.True,"extended floor has collision");
        player.stamina=63;
        s.exitRight=true; player.x=420; Assert.That(s.ExitDirection,Is.EqualTo(1));
        var next = new PhaseController(new[] { new Block(0,160,320,24) },32,149);
        next.ResizeRoom(427); next.EnterFrom(s,-1); Assert.That(next.player.x,Is.EqualTo(419));
        s.ResizeRoom(83);
        Assert.That(s.Width,Is.EqualTo(320)); Assert.That(player.x,Is.EqualTo(312)); Assert.That(s.blocks[0].w,Is.EqualTo(320));
        Assert.That(s.player,Is.SameAs(player)); Assert.That(s.phase,Is.EqualTo(2)); Assert.That(s.jumps,Is.EqualTo(3)); Assert.That(player.stamina,Is.EqualTo(63));
    }
    [Test]
    public void JumpCyclesOnceAndDashKeepsColor()
    {
        var s = Arena(); Ticks(s,1,new Controls { jump=true,jumpHeld=true }); Assert.That(s.phase,Is.EqualTo(1));
        Ticks(s,1,new Controls { jump=true,jumpHeld=true }); Assert.That(s.jumps,Is.EqualTo(1));
        Ticks(s,20,new Controls { dash=true,y=-1 }); Assert.That(s.phase,Is.EqualTo(1));
        for (int phase=0;phase<3;phase++)
        {
            s=Arena();s.phase=phase;s.player.x=192;s.player.y=83;s.player.grounded=false;s.player.coyote=0;
            Ticks(s,1,new Controls { x=1,jump=true,jumpHeld=true });Assert.That(s.phase,Is.EqualTo((phase+1)%3));Assert.That(s.jumps,Is.EqualTo(1));Assert.That(s.player.vx,Is.EqualTo(-130));
        }
    }
    [Test]
    public void MaterializationDefersUntilExit()
    {
        var s=Arena();s.blocks.Add(new Block(67,64,8,40,1));s.player.y=67;s.player.grounded=false;
        Ticks(s,1,new Controls { jump=true,jumpHeld=true });Assert.That(s.blocked.Contains(2),Is.True);Assert.That(s.player.y,Is.LessThan(67));
        Ticks(s,30,new Controls { x=-1,jumpHeld=true });Assert.That(s.blocked.Contains(2),Is.False);
        Assert.That(s.Solid().Contains(s.blocks[2]),Is.True);
    }
    [Test]
    public void VariableJumpCoyoteAndHeldBuffers()
    {
        var tall=Arena();var shortJump=Arena();var jump=new Controls { jump=true,jumpHeld=true };
        Ticks(tall,1,jump);Ticks(shortJump,1,jump);Ticks(tall,18,new Controls { jumpHeld=true });Ticks(shortJump,18);
        Assert.That(tall.player.y,Is.LessThan(shortJump.player.y-8));
        for(int delay=0;delay<8;delay++)
        {
            var s=new PhaseController(new[] { new Block(0,150,36,8) },28,139);s.player.vx=90;
            for(int i=0;i<40&&s.player.grounded;i++)s.Step(new Controls { x=1 });
            Ticks(s,delay,new Controls { x=1 });Ticks(s,1,new Controls { x=1,jump=true,jumpHeld=true });
            Assert.That(s.jumps,Is.EqualTo(delay<5?1:0),"coyote delay "+delay);
        }
        foreach(bool held in new[] { true,false })
        {
            var s=Arena();s.player.y=136;s.player.vy=53.333333333333;s.player.grounded=false;s.player.coyote=0;
            Ticks(s,1,jump);Ticks(s,5,new Controls { jumpHeld=held });Assert.That(s.jumps,Is.EqualTo(held?1:0));
            s=Arena();s.player.y=136;s.player.vy=53.333333333333;s.player.grounded=false;s.player.coyote=0;s.player.dash=0;
            Ticks(s,1,new Controls { x=1,dash=true,dashHeld=true });Ticks(s,5,new Controls { x=1,dashHeld=held });Assert.That(s.dashes,Is.EqualTo(held?1:0));Assert.That(s.phase,Is.Zero);
        }
    }
    [Test]
    public void ClimbEntryStaminaHopAndRefill()
    {
        var s=Arena();var p=s.player;p.x=192;p.y=83;p.vy=160;p.grounded=false;p.coyote=0;
        Ticks(s,1,new Controls { x=1,grab=true });Assert.That(p.climbing,Is.True);Assert.That(p.vy,Is.EqualTo(32));
        s=Arena();p=s.player;p.x=192;p.y=83;p.grounded=false;p.coyote=0;
        Ticks(s,7,new Controls { x=1,grab=true });double stamina=p.stamina;
        Ticks(s,60,new Controls { x=1,grab=true });Assert.That(stamina-p.stamina,Is.EqualTo(10).Within(1e-6));Assert.That(s.phase,Is.Zero);
        s=Arena();p=s.player;p.x=192;p.y=56;p.grounded=false;p.coyote=0;p.dash=0;
        for(int i=0;i<140&&!p.grounded;i++)s.Step(new Controls { x=1,y=-1,grab=true });
        Assert.That(p.grounded,Is.True);Assert.That(p.y,Is.EqualTo(22));Assert.That(p.dash,Is.EqualTo(1));Assert.That(s.phase,Is.Zero);
        foreach(int facing in new[]{-1,1})
        {
            var wall=new Block(facing>0?200:184,0,8,180);s=new PhaseController(new[]{wall},192,40);p=s.player;
            p.grounded=false;p.facing=facing;p.stamina=.1;p.climbing=true;
            Ticks(s,1,new Controls{grab=true});Assert.That(p.climbing,Is.False,"exhaustion releases the grip");
            Ticks(s,90,new Controls{grab=true});Assert.That(p.vy,Is.EqualTo(45));Assert.That(p.y,Is.InRange(103,107),"held exhausted grab remains a slow slide beyond the normal slide timer");
            Ticks(s,8,new Controls());Assert.That(p.vy,Is.GreaterThan(100),"release permits normal fall");
            p.y=40;p.vy=0;Ticks(s,8,new Controls{grab=true,y=1});Assert.That(p.vy,Is.GreaterThan(100),"down bypasses slow slide");
            p.y=40;p.vy=0;Ticks(s,8,new Controls{grab=true,x=-facing});Assert.That(p.vy,Is.GreaterThan(100),"moving away permits normal fall");
            s=new PhaseController(new[]{wall,new Block(0,160,320,24)},192,120);p=s.player;p.grounded=false;p.stamina=0;p.facing=facing;
            Ticks(s,100,new Controls{grab=true});Assert.That(p.grounded,Is.True);Ticks(s,1);Assert.That(p.stamina,Is.EqualTo(110),"landing refills");
        }
    }
    [Test]
    public void FastLandingKicksUpNeutralDustOnlyOnce()
    {
        foreach (double speed in new[] { 60.0, 160.0 })
        {
            var room = new PhaseController(new[] { new Block(0,160,320,24) },120,148);
            room.player.grounded=false;room.player.vy=speed;room.Step(new Controls());
            Assert.That(room.player.grounded,Is.True);
            Assert.That(room.particles.Count,Is.EqualTo(speed>=120?6:0));
            foreach(var dust in room.particles)
            {
                Assert.That(dust.phase,Is.EqualTo(-1),"landing does not suggest a phase change");
                Assert.That(dust.y,Is.LessThan(160));Assert.That(dust.vy,Is.LessThan(0),"dust lifts off the floor");
            }
            int count=room.particles.Count;Ticks(room,5);Assert.That(room.particles.Count,Is.EqualTo(count),"standing does not keep spawning dust");
        }
    }
    static PhaseController Board(bool returns=false, int direction=1)
    {
        double from=direction>0?72:216,to=direction>0?216:72;
        return new PhaseController(Array.Empty<Block>(),from+16,61,new MovingPlatform(new[] { new[] {from,72.0},new[] {to,72.0} },48,8,returns));
    }
    [Test]
    public void PlatformCarryArrivalAndJumpCancellation()
    {
        foreach(int direction in new[] {-1,1}) foreach(bool returns in new[] {false,true})
        {
            var s=Board(returns,direction);Ticks(s,1,new Controls { x=direction,dash=true });
            for(int i=0;i<14;i++){Ticks(s,1,new Controls { x=direction });Assert.That(s.player.x-s.mover.x,Is.EqualTo(16));Assert.That(s.player.y+s.player.h,Is.EqualTo(s.mover.y));}
            Assert.That(s.phase,Is.Zero);Assert.That(s.deaths,Is.Zero);
        }
        var ride=Board(true);ride.mover.x=208;ride.player.x=224;Ticks(ride,1,new Controls { x=1,dash=true });
        for(int i=0;i<14;i++){Ticks(ride,1,new Controls { x=1 });Assert.That(ride.player.x-ride.mover.x,Is.EqualTo(16));}
        Assert.That(ride.mover.moving,Is.False);
        ride=Board();Ticks(ride,1,new Controls { x=1,dash=true });Ticks(ride,6,new Controls { x=1 });Ticks(ride,1,new Controls { x=1,jump=true,jumpHeld=true });
        Assert.That(ride.phase,Is.EqualTo(1));Assert.That(ride.player.y+ride.player.h,Is.LessThan(ride.mover.y));Assert.That(ride.player.x-ride.mover.x,Is.GreaterThan(16));
        ride=Board();Ticks(ride,1,new Controls { x=-1,dash=true });Ticks(ride,8,new Controls { x=-1 });Assert.That(ride.player.x-ride.mover.x,Is.LessThan(16));
        ride=Board();Ticks(ride,1,new Controls { x=1,y=-1,dash=true });Ticks(ride,8,new Controls { x=1,y=-1 });Assert.That(ride.player.y+ride.player.h,Is.LessThan(ride.mover.y));
        ride=Board();Ticks(ride,1,new Controls { x=1,dash=true });while(ride.player.dashTime>0)Ticks(ride,1,new Controls { x=1 });double offset=ride.player.x-ride.mover.x;Ticks(ride,4,new Controls { x=1 });Assert.That(ride.player.x-ride.mover.x,Is.GreaterThan(offset));
    }
    [TestCase(-1,-1)] [TestCase(0,-1)] [TestCase(1,-1)] [TestCase(-1,0)]
    [TestCase(1,0)] [TestCase(-1,1)] [TestCase(0,1)] [TestCase(1,1)]
    public void EightWayAimSamplesAfterFreeze(int x,int y)
    {
        var s=new PhaseController(Array.Empty<Block>(),120,67);s.player.grounded=false;s.player.coyote=0;
        Ticks(s,1,new Controls { x=1,dash=true });Assert.That(s.player.vx,Is.Zero);
        Ticks(s,4,new Controls { x=x,y=y });double length=Math.Sqrt(x*x+y*y);
        Assert.That(s.player.vx,Is.EqualTo(x/length*240).Within(1e-6));Assert.That(s.player.vy,Is.EqualTo(y/length*240).Within(1e-6));Assert.That(s.phase,Is.Zero);
    }
    [Test]
    public void PixelRoundingAndImpactRemainders()
    {
        Assert.That(PhaseController.PixelRound(.5),Is.Zero);Assert.That(PhaseController.PixelRound(1.5),Is.EqualTo(2));Assert.That(PhaseController.PixelRound(-1.5),Is.EqualTo(-2));
        var s=Arena();s.player.x=192;s.player.vx=90;Ticks(s,1,new Controls { x=1 });Assert.That(s.player.remX,Is.Zero);
        Ticks(s,1,new Controls { x=-1 });Assert.That(s.player.x,Is.EqualTo(192));
    }
    [TestCase(-1)] [TestCase(1)]
    public void OnlyConnectedEdgesAllowCrossing(int direction)
    {
        foreach (bool open in new[] { false, true })
        {
            var s=Arena();s.exitLeft=open&&direction<0;s.exitRight=open&&direction>0;
            s.player.x=direction<0?0:312;s.player.vx=direction*90;
            Ticks(s,1,new Controls { x=direction });
            Assert.That(s.ExitDirection,Is.EqualTo(open?direction:0));
            Assert.That(s.player.x,open?(direction<0?Is.LessThan(0):Is.GreaterThan(312)):Is.EqualTo(direction<0?0:312));
            s.player.y=200;Ticks(s,1);
            Assert.That(s.deaths,Is.EqualTo(1));Assert.That(s.ExitDirection,Is.Zero,"death takes priority over travel");
        }
        var ride=new PhaseController(Array.Empty<Block>(),direction<0?0:312,61,
            new MovingPlatform(new[] { new[] { direction<0?-16.0:296,72 },new[] { direction<0?-32.0:312,72 } },48,8));
        ride.exitLeft=direction<0;ride.exitRight=direction>0;
        Ticks(ride,1,new Controls { x=direction,dash=true });Ticks(ride,6,new Controls { x=direction });
        Assert.That(ride.ExitDirection,Is.EqualTo(direction));Assert.That(ride.deaths,Is.Zero,"platform can carry through an open edge");
    }
    [TestCase(-1)] [TestCase(1)]
    public void ArrivalKeepsAirDashAndColorWithoutOldWallContact(int direction)
    {
        var source=Arena();var p=source.player;p.y=67;p.vx=direction*240;p.vy=-120;
        p.dash=0;p.dashTime=.1;p.stamina=63;p.ducking=true;p.h=6;
        p.climbing=p.hopMover=p.dashRide=true;p.wall=p.hopX=p.forceX=1;p.wallLock=p.retention=.1;
        source.phase=2;source.jumps=4;source.dashes=3;source.deaths=1;source.time=12;
        var next=Arena();next.EnterFrom(source,direction);
        Assert.That(next.player.x,Is.EqualTo(direction>0?0:312));Assert.That(next.player.y,Is.EqualTo(67));
        Assert.That((next.player.vx,next.player.vy,next.player.dash,next.player.dashTime,next.player.stamina),Is.EqualTo((direction*240.0,-120.0,0,.1,63.0)));
        Assert.That((next.phase,next.jumps,next.dashes,next.deaths,next.time),Is.EqualTo((2,4,3,1,12.0)));
        Assert.That(next.player.ducking,Is.True);Assert.That(next.player.h,Is.EqualTo(6));Assert.That(next.player.grounded,Is.False);
        Assert.That(next.player.climbing||next.player.hopMover||next.player.dashRide,Is.False);
        Assert.That(next.player.wall+next.player.hopX+next.player.forceX+next.player.wallLock+next.player.retention,Is.Zero);
        Assert.That(next.ExitDirection,Is.Zero,"arrival does not bounce back");
        next.Reset();Assert.That(next.player.x,Is.EqualTo(67));Assert.That(next.phase,Is.Zero);
    }

}
