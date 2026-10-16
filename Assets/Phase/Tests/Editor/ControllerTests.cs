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
                previous = a; var e = c.expected[i]; string frame = c.name + " frame " + (i + 1);
                Assert.That(p.x + 4, Is.EqualTo(e.x), frame + " x"); Assert.That(p.y + p.h, Is.EqualTo(e.y), frame + " y");
                Assert.That(p.vx, Is.EqualTo(e.vx).Within(.002), frame + " vx"); Assert.That(p.vy, Is.EqualTo(e.vy).Within(.002), frame + " vy");
                if (name != "movement") Assert.That(p.stamina, Is.EqualTo(e.stamina).Within(.002), frame + " stamina");
            }
        }
    }
    static PhaseController Arena(double width = 320) => new PhaseController(new[] { new Block(0,150,width,8), new Block(200,33,8,117) }, 67,139);
    static void Ticks(PhaseController s, int count, Controls input = default)
    { for (int i = 0; i < count; i++) { var a = input; a.jump &= i == 0; a.dash &= i == 0; s.Step(a); } }
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
