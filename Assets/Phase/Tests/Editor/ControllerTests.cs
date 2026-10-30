using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Newtonsoft.Json;
using Phase;

public class ControllerTests
{
    [Serializable] public class XY { public double x, y; }
    [Serializable] public class Initial { public XY pos, speed; public bool on_ground; public bool facing = true; }
    [Serializable] public class Solid { public double x, y, width, height; }
    [Serializable] public class Input { public bool up, down, left, right, jump; }
    [Serializable] public class Expected { public double x, y, vx, vy; }
    [Serializable] public class Case { public string name; public Initial initial; public Solid floor; public Solid[] solids; public Input[] inputs; public Expected[] expected; }
    [Serializable] public class Fixture { public double dt; public Case[] cases; }

    [TestCase("movement")]
    [TestCase("actions")]
    public void ImmutableReferenceTraces(string name)
    {
        var f = JsonConvert.DeserializeObject<Fixture>(File.ReadAllText("Assets/Phase/Tests/Fixtures/reference-" + name + ".json"));
        foreach (var c in f.cases)
        {
            var blocks = new List<Block>();
            foreach (var b in c.solids ?? new[] { c.floor }) for (int phase = 0; phase < 3; phase++) blocks.Add(new Block(b.x, b.y, b.width, b.height, phase));
            var init = c.initial; var s = new PhaseController(blocks, init.pos.x - 4, init.pos.y - 11);
            var p = s.player; p.vx = init.speed.x; p.vy = init.speed.y; p.grounded = init.on_ground;
            p.coyote = p.grounded ? .1 : 0; p.facing = init.facing ? 1 : -1;
            var previous = new Input();
            for (int i = 0; i < c.inputs.Length; i++)
            {
                var a = c.inputs[i]; s.Step(new Controls { x = name == "movement" ? 1 : (a.right ? 1 : 0) - (a.left ? 1 : 0), jump = a.jump && !previous.jump, jumpHeld = a.jump }, f.dt);
                previous = a; var e = c.expected[i];
                string frame = c.name + " frame " + (i + 1);
                Assert.That(p.x + 4, Is.EqualTo(e.x), frame + " x"); Assert.That(p.y + p.h, Is.EqualTo(e.y), frame + " y");
                Assert.That(p.vx, Is.EqualTo(e.vx).Within(.002), frame + " vx"); Assert.That(p.vy, Is.EqualTo(e.vy).Within(.002), frame + " vy");
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
            Assert.That(sweep.CheckSpikes(from),Is.True,"a fast move cannot tunnel through a thin root");
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
    public void SpikesActivateOnJumpSweepFastCrossingsAndReset()
    {
        var blue=new Spike(120,100,8,SpikeDirection.Up,2);
        var room=new PhaseController(Array.Empty<Block>(),32,40,spikes:new[]{blue});room.player.x=120;room.player.y=100;room.player.grounded=false;room.player.coyote=.1;
        room.Step(new Controls{jump=true});Assert.That(room.phase,Is.EqualTo(1));Assert.That(room.deaths,Is.EqualTo(1));Assert.That(room.respawn,Is.GreaterThan(0));
        Ticks(room,20);Assert.That(room.player.x,Is.EqualTo(32));Assert.That(room.phase,Is.Zero);
        room=new PhaseController(Array.Empty<Block>(),32,40,spikes:new[]{new Spike(40,40,16,SpikeDirection.Left,1)});
        room.player.grounded=false;room.player.vx=240;
        room.Step(new Controls(),.1);Assert.That(room.deaths,Is.EqualTo(1),"a fast crossing of the complete strip cannot tunnel");

    }
    static PhaseController Arena(double width = 320) => new PhaseController(new[] { new Block(0,150,width,8), new Block(200,33,8,117) }, 67,139);
    static void Ticks(PhaseController s, int count, Controls input = default)
    { for (int i = 0; i < count; i++) { var a = input; a.jump &= i == 0; s.Step(a); } }
    [Test]
    public void AdaptiveRoomExtendsFloorAndEdgesWithoutResettingState()
    {
        var s = new PhaseController(new[] { new Block(0,160,320,24), new Block(240,80,8,80) },32,149);
        var player = s.player; s.phase = 2; s.jumps = 3;
        s.ResizeRoom(427);
        Assert.That(s.blocks[0].w,Is.EqualTo(427)); Assert.That(s.blocks[1].x,Is.EqualTo(240));
        player.x=400; Ticks(s,2); Assert.That(player.grounded,Is.True,"extended floor has collision");
        s.exitRight=true; player.x=420; Assert.That(s.ExitDirection,Is.EqualTo(1));
        var next = new PhaseController(new[] { new Block(0,160,320,24) },32,149);
        next.ResizeRoom(427); next.EnterFrom(s,-1); Assert.That(next.player.x,Is.EqualTo(419));
        s.ResizeRoom(83);
        Assert.That(s.Width,Is.EqualTo(320)); Assert.That(player.x,Is.EqualTo(312)); Assert.That(s.blocks[0].w,Is.EqualTo(320));
        Assert.That(s.player,Is.SameAs(player)); Assert.That(s.phase,Is.EqualTo(2)); Assert.That(s.jumps,Is.EqualTo(3));
    }
    [Test]
    public void EveryJumpIncludingWallJumpsCyclesOnce()
    {
        var s = Arena(); Ticks(s,1,new Controls { jump=true,jumpHeld=true }); Assert.That(s.phase,Is.EqualTo(1));
        Ticks(s,1,new Controls { jump=true,jumpHeld=true }); Assert.That(s.jumps,Is.EqualTo(1));
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
        }
    }
    [Test]
    public void PressingIntoALedgeStepsUpWithinFourPixelsOntoAClearActiveSpot()
    {
        // The player touches the left face of a 40px ledge whose top is y=100, with its feet `below` pixels lower.
        PhaseController Ledge(double below, int mask = 7, Spike spike = null, Block ceiling = null)
        {
            var blocks = new List<Block> { new Block(100,100,40,40,phaseMask:mask) }; if (ceiling != null) blocks.Add(ceiling);
            var s = new PhaseController(blocks,40,40,spikes:spike == null ? null : new[] { spike });
            var p = s.player; p.x = 92; p.y = 100 + below - p.h; p.vx = 90; p.vy = 0; p.grounded = false; p.coyote = 0;
            return s;
        }
        foreach (int below in new[] { 1, 4 })
        {
            var s = Ledge(below); Ticks(s,3,new Controls { x=1 });
            Assert.That(s.player.x,Is.GreaterThan(92),$"{below}px below steps up");
            Assert.That(s.player.y+s.player.h,Is.EqualTo(100),"the feet rest on the ledge top");
            Assert.That(s.deaths,Is.Zero);
        }
        var deep = Ledge(5); Ticks(deep,2,new Controls { x=1 });
        Assert.That(deep.player.x+deep.player.w,Is.EqualTo(100),"5px below stays against the wall");
        var drift = Ledge(3); Ticks(drift,2);
        Assert.That(drift.player.x+drift.player.w,Is.EqualTo(100),"momentum without pressing toward the ledge does not step up");
        var inactive = Ledge(3,mask:2); Ticks(inactive,2,new Controls { x=1 });
        Assert.That(inactive.player.y+inactive.player.h,Is.GreaterThanOrEqualTo(103),"a ledge of another color is not stepped onto");
        var spiked = Ledge(3,spike:new Spike(100,92,8,SpikeDirection.Up)); Ticks(spiked,2,new Controls { x=1 });
        Assert.That((spiked.player.x+spiked.player.w,spiked.deaths),Is.EqualTo((100d,0)),"a spike at the raised spot blocks the step-up");
        var capped = Ledge(3,ceiling:new Block(88,82,16,8)); Ticks(capped,2,new Controls { x=1 });
        Assert.That(capped.player.x+capped.player.w,Is.EqualTo(100),"a block over the raised spot blocks the step-up");
    }
    [Test]
    public void HittingACeilingZeroesRiseWithoutReversingOrSticking()
    {
        // Underside 3px (frames into the jump, while the variable-jump hold is still fresh) and 30px above the head.
        foreach (int underside in new[] { 144, 118 })
        {
            var s = new PhaseController(new[] { new Block(0,160,320,24), new Block(0,underside-16,320,16) },120,148);
            var p = s.player; p.grounded = true; p.coyote = .1;
            int hit = -1, falling = -1;
            for (int i = 0; i < 40; i++)
            {
                s.Step(new Controls { jump = i == 0, jumpHeld = true });
                Assert.That(p.y, Is.GreaterThanOrEqualTo(underside), $"underside {underside}: never inside the ceiling");
                if (hit < 0 && i > 0 && p.vy >= 0) hit = i;
                if (hit >= 0 && falling < 0 && p.vy > 0) falling = i;
                if (hit >= 0 && i <= hit + 1) Assert.That(p.vy, Is.LessThan(20), $"underside {underside}: no downward kick on impact");
            }
            Assert.That(hit, Is.GreaterThan(0), $"underside {underside}: the rise ends at the ceiling");
            Assert.That(falling - hit, Is.LessThanOrEqualTo(1), $"underside {underside}: gravity takes over at once, no sticking");
        }
    }
    [Test]
    public void RisingIntoACeilingCornerSlidesPastItAndKeepsRising()
    {
        var s = new PhaseController(new[] { new Block(0,160,320,24), new Block(90,120,40,16) },120,149);
        var p = s.player; p.grounded = true; p.coyote = .1;
        Ticks(s,1,new Controls { jump = true, jumpHeld = true, x = 1 });
        for (int i = 0; i < 12; i++) s.Step(new Controls { jumpHeld = true, x = 1 });
        Assert.That(p.y, Is.LessThan(136), "the corner nudge lets the rise continue past the slab underside");
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
    }

    [Test]
    public void RespawnClampsAWideRoomCheckpointAfterViewportShrinks()
    {
        var c = new PhaseController(new[] { new Block(0,160,320,24) },408,149);
        c.exitRight = true; c.ResizeRoom(427); c.Reset();
        Assert.That(c.player.x,Is.EqualTo(408));
        c.ResizeRoom(320); c.player.y = 200;
        Ticks(c,30,default);
        Assert.That(c.deaths,Is.EqualTo(1));
        Assert.That(c.player.x,Is.EqualTo(312));
        Assert.That(c.player.y,Is.EqualTo(149));
        Assert.That(c.ExitDirection,Is.Zero,"respawn must not travel through the newly narrowed edge");
    }

}
