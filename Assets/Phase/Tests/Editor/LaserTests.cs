using System;
using NUnit.Framework;
using Phase;

public class LaserTests
{
    [Test]
    public void EveryColorAndDirectionUsesItsPhaseMask()
    {
        foreach (LaserDirection direction in Enum.GetValues(typeof(LaserDirection)))
        for (int mask = 1; mask <= 7; mask++) for (int phase = 0; phase < 3; phase++)
        {
            var laser = new Laser(120, 80, direction, mask);
            var s = new PhaseController(Array.Empty<Block>(), 32, 149, lasers: new[] { laser }); s.phase = phase;
            var b = s.LaserBeam(laser); s.player.x = b.x + b.w / 2 - 4; s.player.y = b.y + b.h / 2 - 5;
            Assert.That(s.CheckHazards(), Is.EqualTo((mask & (1 << phase)) != 0), $"mask {mask}, phase {phase}, {direction}");
            s.CheckHazards(); Assert.That(s.deaths, Is.LessThanOrEqualTo(1));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new Laser(0, 0, (LaserDirection)4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Laser(0, 0, LaserDirection.Right, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Laser(0, 0, LaserDirection.Right, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Laser(double.NaN, 0, LaserDirection.Right));
    }

    [Test]
    public void BeamsEndAtTheNearestSolidInAllFourDirections()
    {
        foreach (LaserDirection direction in Enum.GetValues(typeof(LaserDirection)))
        {
            var laser = new Laser(120, 80, direction);
            var solids = new[] { new Block(40, 80, 8, 8), new Block(200, 80, 8, 8), new Block(120, 24, 8, 8), new Block(120, 144, 8, 8),
                new Block(80, 80, 8, 8), new Block(160, 80, 8, 8), new Block(120, 48, 8, 8), new Block(120, 112, 8, 8) };
            var b = laser.Beam(solids, 320);
            Assert.That(new[] { b.x, b.y, b.w, b.h }, Is.EqualTo(direction == LaserDirection.Up ? new double[] {122,56,4,24} :
                direction == LaserDirection.Down ? new double[] {122,88,4,24} : direction == LaserDirection.Left ? new double[] {88,82,32,4} : new double[] {128,82,32,4}));
            var hidden = laser.Beam(new[] { new Block(0, 0, 320, 180) }, 320);
            Assert.That(hidden.w * hidden.h, Is.Zero, "an embedded mouth cannot shoot through its solid");
            var empty = laser.Beam(Array.Empty<Block>(), 427);
            Assert.That(empty.x + empty.w, Is.LessThanOrEqualTo(427)); Assert.That(empty.y + empty.h, Is.LessThanOrEqualTo(180));
            if (direction == LaserDirection.Right) Assert.That(empty.x + empty.w, Is.EqualTo(427));
        }
        var outside = new Laser(-16, 80, LaserDirection.Right).Beam(Array.Empty<Block>(),320);
        Assert.That(outside.w * outside.h, Is.Zero);
    }

    [Test]
    public void GhostAndDeferredTerrainDoNotShieldThePlayer()
    {
        var laser = new Laser(8, 80, LaserDirection.Right);
        var s = new PhaseController(new[] { new Block(80,80,8,8,0), new Block(160,80,8,8) }, 100, 80, lasers: new[] { laser });
        Assert.That(s.LaserBeam(laser).w, Is.EqualTo(64)); Assert.That(s.CheckHazards(), Is.False);
        s.phase = 1; Assert.That(s.LaserBeam(laser).w, Is.EqualTo(144)); Assert.That(s.CheckHazards(), Is.True);
        s.Reset(); s.blocked.Add(0); Assert.That(s.LaserBeam(laser).w, Is.EqualTo(144));
        Assert.That(s.CheckHazards(), Is.True);
    }


    [Test]
    public void AnInactiveBeamIsStoppedOnlyByWhatIsSolidInItsNextPhase()
    {
        var blue = new Laser(8, 80, LaserDirection.Right, 2); var green = new Laser(8, 120, LaserDirection.Right, 4);
        var platform = new Platform(false, 1, new[] { (10, 10), (10, 11) }, phaseMask: 1);
        var s = new PhaseController(new[] { new Block(80,120,8,8,phaseMask:1), new Block(160,120,8,8,phaseMask:4) }, 100, 40,
            lasers: new[] { blue, green }, platforms: new[] { platform });
        Assert.That(s.LaserBeam(blue).w, Is.EqualTo(304), "red platform does not stop a blue beam");
        Assert.That(s.LaserBeam(green).w, Is.EqualTo(144), "green beam skips red terrain and stops at green terrain");
        s.phase = 1; Assert.That(s.LaserBeam(green).w, Is.EqualTo(144));
        s.phase = 2; Assert.That(s.LaserBeam(blue).w, Is.EqualTo(304));
    }

    [Test]
    public void SweepsCatchFastCrossingsAndIgnoreTangency()
    {
        var laser=new Laser(120,0,LaserDirection.Down);
        foreach(int sign in new[]{-1,1})
        {
            var s=new PhaseController(Array.Empty<Block>(),120+sign*40,80,lasers:new[]{laser});
            var from=new Block(120-sign*40,80,8,11);
            Assert.That(s.CheckHazards(from),Is.True,"dash/carry cannot tunnel through a beam");
        }
        var tangent=new PhaseController(Array.Empty<Block>(),114,80,lasers:new[]{laser});
        Assert.That(tangent.CheckHazards(new Block(114,40,8,11)),Is.False,"edge contact is not overlap");
    }

    [Test]
    public void JumpActivatesTheLaserAndRespawnRestoresTheRoom()
    {
        var laser=new Laser(8,80,LaserDirection.Right,2);
        var s=new PhaseController(Array.Empty<Block>(),100,80,lasers:new[]{laser});
        s.Step(new Controls{jump=true,jumpHeld=true});
        Assert.That(s.phase,Is.EqualTo(1)); Assert.That(s.deaths,Is.EqualTo(1)); Assert.That(s.respawn,Is.GreaterThan(0));
        for(int i=0;i<20;i++)s.Step(default);
        Assert.That(s.phase,Is.Zero); Assert.That(s.respawn,Is.Zero);
        Assert.That(s.lasers.Count,Is.EqualTo(1));
    }


    [Test]
    public void BeamGeometryHasNoTimeCycle()
    {
        var laser=new Laser(80,8,LaserDirection.Down,4);
        var s=new PhaseController(new[]{new Block(0,160,320,24)},32,149,lasers:new[]{laser});
        var before=s.LaserBeam(laser);
        for(int i=0;i<600;i++)s.Step(default);
        var after=s.LaserBeam(laser);
        Assert.That(new[]{after.x,after.y,after.w,after.h},Is.EqualTo(new[]{before.x,before.y,before.w,before.h}));
        Assert.That(s.deaths,Is.Zero);
    }
}
