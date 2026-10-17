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
        foreach (CrystalKind kind in Enum.GetValues(typeof(CrystalKind)))
        for (int mask = 1; mask <= 7; mask++) for (int phase = 0; phase < 3; phase++)
        {
            var crystal = new Crystal(40, 80, kind, mask);
            var s = Airborne(crystal); s.phase = phase; s.player.dash = 0;
            s.Step(default);
            bool active = (mask & (1 << phase)) != 0;
            Assert.That(crystal.collected, Is.EqualTo(active), $"{kind}, mask {mask}, phase {phase}");
            Assert.That(s.player.airJump, Is.EqualTo(active && kind == CrystalKind.Jump ? 1 : 0));
            Assert.That(s.player.dash, Is.EqualTo(active && kind == CrystalKind.Dash ? 1 : 0));
            Assert.That(s.phase, Is.EqualTo(phase), "pickup never changes phase");
            Assert.That(new[] { crystal.w, crystal.h }, Is.EqualTo(new double[] { 8, 8 }));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new Crystal(0, 0, (CrystalKind)2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Crystal(0, 0, CrystalKind.Jump, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Crystal(0, 0, CrystalKind.Jump, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Crystal(double.NaN, 0, CrystalKind.Jump));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Crystal(0, double.PositiveInfinity, CrystalKind.Jump));
    }

    [Test]
    public void CircleGivesOneOrdinaryAirJumpAndCyclesPhaseWhenUsed()
    {
        var s = Airborne(new Crystal(40, 80, CrystalKind.Jump), new Crystal(40, 80, CrystalKind.Jump));
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
            var s = Airborne(new Crystal(40, 80, CrystalKind.Jump));
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
        var s = Airborne(new Crystal(40, 80, CrystalKind.Jump));
        s.Step(new Controls { jumpHeld = true });
        Assert.That(s.player.airJump, Is.EqualTo(1)); Assert.That(s.jumps, Is.Zero);
        s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(s.jumps, Is.EqualTo(1)); Assert.That(s.phase, Is.EqualTo(1)); Assert.That(s.player.airJump, Is.Zero);

        s = Airborne(new Crystal(40, 80, CrystalKind.Jump));
        s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(s.jumps, Is.Zero, "the pickup frame cannot spend its own fresh input");
        s.Step(new Controls { jumpHeld = true });
        Assert.That(s.jumps, Is.Zero, "holding alone never spends the bonus");
        s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(s.jumps, Is.EqualTo(1), "a later fresh edge is enough even without a neutral physics step");
    }

    [Test]
    public void FreshBonusPressDuringDashFreezeUsesTheExistingJumpBuffer()
    {
        var s = Airborne(new Crystal(40, 80, CrystalKind.Jump)); s.Step(default);
        s.freeze = .05; s.player.dashTime = .15; s.player.dx = 1; s.player.vx = 240;
        s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(s.jumps, Is.Zero);
        for (int i = 0; i < 4; i++) s.Step(new Controls { jumpHeld = true });
        Assert.That(s.jumps, Is.EqualTo(1)); Assert.That(s.phase, Is.EqualTo(1));
        Assert.That(s.player.airJump, Is.Zero); Assert.That(s.player.dashTime, Is.Zero);
    }

    [Test]
    public void BufferedPressBeforePickupDuringFreezeCannotSpendTheNewBonus()
    {
        var crystal = new Crystal(80, 80, CrystalKind.Jump);
        var s = Airborne(crystal); s.freeze = .05;
        s.Step(new Controls { jump = true, jumpHeld = true });
        s.player.x = 80;
        for (int i = 0; i < 4; i++) s.Step(new Controls { jumpHeld = true });
        Assert.That(crystal.collected, Is.True); Assert.That(s.player.airJump, Is.EqualTo(1));
        Assert.That(s.jumps, Is.Zero, "a pre-pickup buffered press cannot use the new bonus");
        s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(s.jumps, Is.EqualTo(1));
    }

    [Test]
    public void GroundCoyoteAndWallJumpsPreserveTheBonus()
    {
        var floor = new Block(0, 160, 320, 24);
        var ground = new PhaseController(new[] { floor }, 40, 149, crystals: new[] { new Crystal(40, 149, CrystalKind.Jump) });
        var coyote = Airborne(new Crystal(40, 80, CrystalKind.Jump)); coyote.player.coyote = .1;
        var wall = new PhaseController(new[] { new Block(48, 40, 8, 120) }, 40, 80,
            crystals: new[] { new Crystal(40, 80, CrystalKind.Jump) });
        wall.player.grounded = false; wall.player.coyote = 0;
        foreach (var s in new[] { ground, coyote, wall })
        {
            s.Step(default); s.Step(new Controls { jump = true, jumpHeld = true });
            Assert.That(s.jumps, Is.EqualTo(1)); Assert.That(s.player.airJump, Is.EqualTo(1));
        }
    }

    [Test]
    public void SquareConsumesEvenWhenFullAndDoesNotCancelTheDashOrRefillStamina()
    {
        foreach (int remaining in new[] { 0, 1 }) foreach (bool dashing in new[] { false, true })
        {
            var crystal = new Crystal(40, 80, CrystalKind.Dash);
            var s = Airborne(crystal); var p = s.player; s.phase = 2;
            p.dash = remaining; p.stamina = 42;
            if (dashing) { p.dashTime = .15; p.dashAttack = .3; p.dx = 1; p.vx = 240; }
            s.Step(default);
            Assert.That(p.dash, Is.EqualTo(1)); Assert.That(crystal.collected, Is.True);
            Assert.That(p.stamina, Is.EqualTo(42)); Assert.That(s.phase, Is.EqualTo(2));
            Assert.That(p.dashTime > 0, Is.EqualTo(dashing));
        }
    }

    [Test]
    public void BonusJumpDuringDashUsesNormalJumpSpeed()
    {
        var s = Airborne(new Crystal(40, 80, CrystalKind.Jump)); s.Step(default);
        var p = s.player; p.dashTime = .15; p.dx = 1; p.vx = 240;
        s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(p.dashTime, Is.Zero); Assert.That(p.vy, Is.EqualTo(-105));
        Assert.That(p.vx, Is.EqualTo(240), "extra jump must not apply the grounded dash-jump boost");
        Assert.That(s.phase, Is.EqualTo(1)); Assert.That(p.airJump, Is.Zero);
    }

    [Test]
    public void BlockedUnduckPreservesBonusWhenAirJumpCannotStart()
    {
        var s = new PhaseController(new[] { new Block(32, 78, 64, 7) }, 40, 85,
            crystals: new[] { new Crystal(40, 85, CrystalKind.Jump) });
        var p = s.player; p.grounded = false; p.coyote = 0; p.ducking = true; p.h = 6;
        p.dashTime = .15; p.dx = 1; p.vx = 240;
        s.Step(default); s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(p.airJump, Is.EqualTo(1)); Assert.That(s.phase, Is.Zero); Assert.That(s.jumps, Is.Zero);
        Assert.That(p.dashTime, Is.GreaterThan(0));
    }

    [Test]
    public void LandingRearmsPickupsAndClearsUnusedBonusButGroundPickupRemainsUsable()
    {
        var crystal = new Crystal(40, 80, CrystalKind.Jump);
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
        var crystal = new Crystal(40, 80, CrystalKind.Jump);
        var s = Airborne(crystal); s.Step(default); s.Reset();
        Assert.That(crystal.collected, Is.False); Assert.That(s.player.airJump, Is.Zero);
        s.Step(default); s.lasers.Add(new Laser(0, 80, LaserDirection.Right));
        Assert.That(s.CheckHazards(), Is.True);
        for (int i = 0; i < 17; i++) s.Step(default);
        Assert.That(s.respawn, Is.Zero); Assert.That(crystal.collected, Is.False); Assert.That(s.player.airJump, Is.Zero);
    }

    [Test]
    public void SweepsCollectDuringFastDashAndPlatformCarry()
    {
        var dashCrystal = new Crystal(64, 80, CrystalKind.Dash);
        var dash = Airborne(dashCrystal); var p = dash.player;
        p.dash = 0; p.dashTime = .15; p.dx = 1; p.vx = 240;
        for (int i = 0; i < 7; i++) dash.Step(default);
        Assert.That(dashCrystal.collected, Is.True); Assert.That(p.dash, Is.EqualTo(1));

        var carryCrystal = new Crystal(60, 109, CrystalKind.Jump);
        var mover = new MovingPlatform(new[] { new double[] { 40, 120 }, new double[] { 120, 120 } }, 24, 8);
        var carry = new PhaseController(Array.Empty<Block>(), 40, 109, new[] { mover, new MovingPlatform(new[] { new double[] { 200, 144 }, new double[] { 240, 144 } }, 24, 8) }, crystals: new[] { carryCrystal });
        foreach (var platform in carry.movers) platform.pending = 1;
        for (int i = 0; i < 6; i++) carry.Step(default);
        Assert.That(carryCrystal.collected, Is.True); Assert.That(carry.player.airJump, Is.EqualTo(1));
        Assert.That(carry.movers[1].x, Is.GreaterThan(200), "other platforms still advance while the rider collects a crystal");
    }

    [Test]
    public void PhaseSwitchCollectsNewlyActiveCrystalButHazardsHavePriority()
    {
        var crystal = new Crystal(40, 80, CrystalKind.Jump, 2);
        var s = Airborne(crystal); s.player.coyote = .1;
        s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(crystal.collected, Is.True); Assert.That(s.player.airJump, Is.EqualTo(1));
        Assert.That(s.phase, Is.EqualTo(1));

        crystal = new Crystal(40, 80, CrystalKind.Dash, 2);
        s = Airborne(crystal); s.player.coyote = .1; s.player.dash = 0;
        s.lasers.Add(new Laser(0, 80, LaserDirection.Right, 2));
        s.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(s.deaths, Is.EqualTo(1)); Assert.That(crystal.collected, Is.False);
        Assert.That(s.player.dash, Is.Zero);
    }

    [Test]
    public void GearCarryCollectsAndAirborneRoomTransitionPreservesTheBonus()
    {
        var crystal = new Crystal(64, 107, CrystalKind.Jump);
        var s = new PhaseController(new[] { new Block(0, 160, 320, 24) }, 64, 120,
            gears: new[] { new ClimbGear(new Block(72, 88, 8, 72), 64, new Block(88, 104, 48, 8), 112) },
            crystals: new[] { crystal });
        var p = s.player; p.grounded = false; p.coyote = 0; p.climbing = true; p.facing = 1; p.climbWait = 0;
        for (int i = 0; i < 5; i++) s.Step(new Controls { x = 1, y = -1, grab = true });
        Assert.That(crystal.collected, Is.True); Assert.That(p.airJump, Is.EqualTo(1));
        Assert.That(s.gears[0].Lift, Is.GreaterThan(0));
        var next = Airborne(); next.EnterFrom(s, 1);
        Assert.That(next.player.airJump, Is.EqualTo(1));
        next.Step(new Controls { jump = true, jumpHeld = true });
        Assert.That(next.player.airJump, Is.Zero); Assert.That(next.phase, Is.EqualTo(1));
    }
}
