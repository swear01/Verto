using System;
using NUnit.Framework;
using Phase;

public class CrystalTests
{
    static PhaseController Airborne(params Crystal[] crystals)
    {
        var s = new PhaseController(Array.Empty<Block>(), 40, 80, crystals: crystals);
        s.player.grounded = false; s.player.coyote = 0;
        return s;
    }

    [Test]
    public void SevenMasksCollectOnlyInMatchingPhases()
    {
        for (int mask = 1; mask <= 7; mask++) for (int phase = 0; phase < 3; phase++)
        {
            var crystal = new Crystal(40, 80, mask);
            var s = Airborne(crystal); s.phase = phase;
            s.Step(default);
            bool active = (mask & (1 << phase)) != 0;
            Assert.That(crystal.collected, Is.EqualTo(active), $"mask {mask}, phase {phase}");
            Assert.That(s.player.airJump, Is.EqualTo(active ? 1 : 0));
            Assert.That(s.phase, Is.EqualTo(phase), "pickup never changes phase");
            Assert.That(new[] { crystal.w, crystal.h }, Is.EqualTo(new double[] { 8, 8 }));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new Crystal(0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Crystal(0, 0, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Crystal(double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Crystal(0, double.PositiveInfinity));
    }

    [Test]
    public void CircleGivesOneOrdinaryAirJumpAndCyclesPhaseWhenUsed()
    {
        var s = Airborne(new Crystal(40, 80), new Crystal(40, 80));
        s.Step(default);
        Assert.That(s.player.airJump, Is.EqualTo(1), "circles do not stack");
        Assert.That(s.player.coyote, Is.Zero); Assert.That(s.player.grounded, Is.False);
        s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(s.player.airJump, Is.Zero); Assert.That(s.jumps, Is.EqualTo(1));
        Assert.That(s.phase, Is.EqualTo(1)); Assert.That(s.player.vy, Is.EqualTo(-105));
        s.Step(default); s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(s.jumps, Is.EqualTo(1), "one pickup cannot supply two jumps before landing");
    }

    [Test]
    public void CollectionWhilePressingOrHoldingJumpRequiresReleaseAndFreshPress()
    {
        foreach (bool pressed in new[] { false, true })
        {
            var s = Airborne(new Crystal(40, 80));
            s.Step(new Controls { jump = pressed, jumpHeld = true });
            for (int i = 0; i < 4; i++) s.Step(new Controls { jumpHeld = true });
            Assert.That(s.jumps, Is.Zero, "old buffered/held input cannot spend a new pickup");
            Assert.That(s.player.airJump, Is.EqualTo(1));
            s.Step(default); s.Step(new Controls { jump = true, jumpHeld = true });
            Assert.That(s.jumps, Is.EqualTo(1));
        }
    }

    [Test]
    public void FreshPressBetweenPhysicsStepsCanSpendAnEarlierPickupWithoutNeutralSample()
    {
        var s = Airborne(new Crystal(40, 80));
        s.Step(new Controls { jumpHeld = true });
        Assert.That(s.player.airJump, Is.EqualTo(1)); Assert.That(s.jumps, Is.Zero);
        s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(s.jumps, Is.EqualTo(1)); Assert.That(s.phase, Is.EqualTo(1)); Assert.That(s.player.airJump, Is.Zero);

        s = Airborne(new Crystal(40, 80));
        s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(s.jumps, Is.Zero, "the pickup frame cannot spend its own fresh input");
        s.Step(new Controls { jumpHeld = true });
        Assert.That(s.jumps, Is.Zero, "holding alone never spends the bonus");
        s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(s.jumps, Is.EqualTo(1), "a later fresh edge is enough even without a neutral physics step");
    }

    [Test]
    public void GroundCoyoteAndWallJumpsPreserveTheBonus()
    {
        var floor = new Block(0, 160, 320, 24);
        var ground = new PhaseController(new[] { floor }, 40, 149, crystals: new[] { new Crystal(40, 149) });
        var coyote = Airborne(new Crystal(40, 80)); coyote.player.coyote = .1;
        var wall = new PhaseController(new[] { new Block(48, 40, 8, 120) }, 40, 80,
            crystals: new[] { new Crystal(40, 80) });
        wall.player.grounded = false; wall.player.coyote = 0;
        foreach (var s in new[] { ground, coyote, wall })
        {
            s.Step(default); s.Step(new Controls { jump = true, jumpHeld = true });
            Assert.That(s.jumps, Is.EqualTo(1)); Assert.That(s.player.airJump, Is.EqualTo(1));
        }
    }

    [Test]
    public void LandingRearmsPickupsAndClearsUnusedBonusButGroundPickupRemainsUsable()
    {
        var crystal = new Crystal(40, 80);
        var s = new PhaseController(new[] { new Block(0, 160, 320, 24) }, 40, 80, crystals: new[] { crystal });
        s.player.grounded = false; s.player.coyote = 0; s.Step(default);
        for (int i = 0; i < 12; i++) s.Step(default);
        Assert.That(crystal.collected, Is.True, "no timer farming while airborne");
        s.player.y = 149; s.player.vy = 20; s.Step(default);
        Assert.That(crystal.collected, Is.False); Assert.That(s.player.airJump, Is.Zero);
        crystal.y = 149; s.Step(default); s.Step(default);
        Assert.That(crystal.collected, Is.True); Assert.That(s.player.airJump, Is.EqualTo(1));
    }

    [Test]
    public void ResetAndDeathRearmCrystalsAndClearBonus()
    {
        var crystal = new Crystal(40, 80);
        var s = Airborne(crystal); s.Step(default); s.Reset();
        Assert.That(crystal.collected, Is.False); Assert.That(s.player.airJump, Is.Zero);
        s.Step(default); s.lasers.Add(new Laser(0, 80, LaserDirection.Right));
        Assert.That(s.CheckHazards(), Is.True);
        for (int i = 0; i < 17; i++) s.Step(default);
        Assert.That(s.respawn, Is.Zero); Assert.That(crystal.collected, Is.False); Assert.That(s.player.airJump, Is.Zero);
    }

    [Test]
    public void PhaseSwitchCollectsNewlyActiveCrystalButHazardsHavePriority()
    {
        var crystal = new Crystal(40, 80, 2);
        var s = Airborne(crystal); s.player.coyote = .1;
        s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(crystal.collected, Is.True); Assert.That(s.player.airJump, Is.EqualTo(1));
        Assert.That(s.phase, Is.EqualTo(1));

        crystal = new Crystal(40, 80, 2);
        s = Airborne(crystal); s.player.coyote = .1;
        s.lasers.Add(new Laser(0, 80, LaserDirection.Right, 2));
        s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(s.deaths, Is.EqualTo(1)); Assert.That(crystal.collected, Is.False);
        Assert.That(s.player.airJump, Is.Zero);
    }

}
