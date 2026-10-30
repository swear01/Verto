using System;
using System.Linq;
using NUnit.Framework;
using Phase;

public class PlatformTests
{
    static readonly Controls None = default, Jump = new Controls { jump = true, jumpHeld = true };
    static Platform Mover(bool reverse = false, int mask = 7, params SpikeDirection[] sides) =>
        new Platform(false, 3, new[] { (10, 5), (20, 5), (20, 3) }, reverse, mask, false, sides);
    static PhaseController Room(Platform platform, params Block[] more) =>
        new PhaseController(new[] { new Block(0, 160, 320, 24) }.Concat(more), 32, 149, platforms: new[] { platform });
    static void Run(PhaseController c, int steps, Controls input = default) { for (int i = 0; i < steps; i++) c.Step(input); }

    [Test]
    public void EveryJumpStepsToTheNextPivotAndSlidesForAboutThreeTenths()
    {
        var platform = Mover(); var c = Room(platform);
        Assert.That((platform.x, platform.y, platform.w, platform.h), Is.EqualTo((72d, 40d, 24d, 8d)), "starts centred on the first pivot");
        c.Step(Jump); Assert.That(c.jumps, Is.EqualTo(1));
        Run(c, 9); Assert.That(platform.x, Is.GreaterThan(72).And.LessThan(152), "halfway through the slide");
        Run(c, 11); Assert.That((platform.x, platform.y, platform.pivot), Is.EqualTo((152d, 40d, 1)));
        // The path is a loop of three: three jumps bring the platform home, independently of the 3-color cycle's own count.
        c.player.grounded = true; c.player.coyote = .1; c.Step(Jump); Run(c, 20);
        Assert.That((platform.x, platform.y, platform.pivot), Is.EqualTo((152d, 24d, 2)));
        c.player.grounded = true; c.player.coyote = .1; c.Step(Jump); Run(c, 20);
        Assert.That((platform.x, platform.y, platform.pivot), Is.EqualTo((72d, 40d, 0)));
        Assert.That(c.jumps, Is.EqualTo(3));
    }

    [Test]
    public void WallAndCrystalJumpsAlsoStepAndReverseWalksTheLoopBackwards()
    {
        var wall = Mover(); var c = Room(wall, new Block(200, 33, 8, 117));
        c.player.x = 192; c.player.y = 100; c.player.grounded = false; c.player.coyote = 0;
        c.Step(new Controls { jump = true, jumpHeld = true, x = 1 });
        Assert.That((c.jumps, wall.queued), Is.EqualTo((1, 1)), "a wall jump counts");
        var crystal = new PhaseController(Array.Empty<Block>(), 100, 80, crystals: new[] { new Crystal(100, 80) }, platforms: new[] { Mover() });
        crystal.player.grounded = false; crystal.player.coyote = 0;
        crystal.Step(None); Assert.That(crystal.player.airJump, Is.EqualTo(1));
        crystal.Step(Jump); Assert.That((crystal.jumps, crystal.platforms[0].queued), Is.EqualTo((1, 1)), "a crystal jump counts");
        var back = Mover(reverse: true); var r = Room(back);
        r.Step(Jump); Run(r, 20);
        Assert.That((back.pivot, back.x, back.y), Is.EqualTo((2, 152d, 24d)), "counter-clockwise visits the last pivot first");
    }

    [Test]
    public void JumpsDuringASlideQueueAndDeathReturnsPlatformsToTheirFirstPivot()
    {
        var platform = Mover(); var c = Room(platform);
        c.Step(Jump); Run(c, 3);
        c.player.grounded = true; c.player.coyote = .1; c.Step(Jump);
        Assert.That(platform.queued, Is.EqualTo(1), "the second step waits its turn");
        Run(c, 16); Assert.That(platform.pivot, Is.EqualTo(1), "the first slide finishes before the next one starts");
        Run(c, 20); Assert.That(platform.pivot, Is.EqualTo(2));
        c.player.y = 400; Run(c, 30);
        Assert.That((c.deaths, platform.pivot, platform.queued, platform.x, platform.y), Is.EqualTo((1, 0, 0, 72d, 40d)));
    }

    [Test]
    public void StandingOnAPlatformRidesIt()
    {
        var platform = new Platform(false, 3, new[] { (6, 18), (14, 18) }, false, 7);
        var c = new PhaseController(Array.Empty<Block>(), 40, 133, platforms: new[] { platform });
        Run(c, 5); Assert.That(c.player.grounded, Is.True, "lands on the platform");
        double x = c.player.x, y = c.player.y; c.player.airJump = 0;
        platform.queued = 1; Run(c, 24);
        Assert.That(platform.x, Is.EqualTo(104d));
        Assert.That((c.player.x - x, c.player.y, c.player.grounded), Is.EqualTo((64d, y, true)), "carried along its whole move");
        // Vertical moves carry too, whether the platform rises or falls.
        var lift = new Platform(false, 3, new[] { (6, 18), (6, 10) }, false, 7);
        var up = new PhaseController(Array.Empty<Block>(), 40, 133, platforms: new[] { lift });
        Run(up, 5); double top = up.player.y; lift.queued = 1; Run(up, 24);
        Assert.That((up.player.y - top, up.player.grounded, up.deaths), Is.EqualTo((-64d, true, 0)));
        lift.queued = 1; Run(up, 24);
        Assert.That((up.player.y, up.player.grounded, up.deaths), Is.EqualTo((top, true, 0)));
    }

    [Test]
    public void ARiderCarriedThroughACrystalInOneStepCollectsIt()
    {
        var platform = new Platform(false, 3, new[] { (6, 18), (14, 18) }, false, 7);
        var crystal = new Crystal(72, 136);
        var c = new PhaseController(Array.Empty<Block>(), 40, 133, crystals: new[] { crystal }, platforms: new[] { platform });
        Run(c, 5); c.player.airJump = 0;
        platform.queued = 1; c.Step(None, Platform.SlideTime);
        Assert.That((c.player.x, crystal.collected, c.player.airJump), Is.EqualTo((104d, true, 1)), "the whole slide passes the crystal in one step");
    }

    [Test]
    public void ACrushedPlayerPassesThroughInsteadOfDying()
    {
        // A wall stands right beside Wick, so the platform sliding into his side cannot push him.
        var platform = new Platform(false, 1, new[] { (6, 19), (12, 19) }, false, 7);
        var c = new PhaseController(new[] { new Block(0, 160, 320, 24), new Block(88, 120, 8, 40) }, 80, 149, platforms: new[] { platform });
        int index = c.blocks.IndexOf(platform);
        double x = c.player.x; platform.queued = 1; bool passed = false;
        for (int i = 0; i < 40; i++)
        {
            c.Step(None);
            passed |= c.blocked.Contains(index);
            Assert.That((c.player.x, c.deaths), Is.EqualTo((x, 0)), $"step {i}: nobody dies and Wick stays put");
        }
        Assert.That(passed, Is.True, "the platform and Wick passed through each other");
        Assert.That((platform.x, c.blocked.Contains(index)), Is.EqualTo((96d, false)), "collision returns once Wick is clear");
        // Pushing a player who has room simply moves him.
        var free = new Platform(false, 1, new[] { (6, 19), (9, 19) }, false, 7);
        var open = new PhaseController(new[] { new Block(0, 160, 320, 24) }, 56, 149, platforms: new[] { free });
        free.queued = 1; Run(open, 30);
        Assert.That((open.player.x, open.deaths, open.blocked.Count), Is.EqualTo((80d, 0, 0)), "a platform with room to push just moves Wick");
    }

    [Test]
    public void AttachedSpikesShareTheColorMoveWithThePlatformAndKill()
    {
        var platform = Mover(mask: 2, sides: new[] { SpikeDirection.Up, SpikeDirection.Left });
        var c = Room(platform);
        Assert.That(c.spikes.Count, Is.EqualTo(2));
        Assert.That(platform.attached.Select(s => (s.x, s.y, s.w, s.h, s.direction, s.Mask)),
            Is.EquivalentTo(new[] { (72d, 32d, 24d, 8d, SpikeDirection.Up, 2), (64d, 40d, 8d, 8d, SpikeDirection.Left, 2) }),
            "the rows sit in the cells beside the body, one cell per cell of the face");
        platform.queued = 1; Run(c, 20);
        Assert.That(platform.attached.Select(s => (s.x, s.y)), Is.EquivalentTo(new[] { (152d, 32d), (144d, 40d) }));
        // A blue row is lethal in the blue phase only.
        var row = new Platform(false, 1, new[] { (4, 18), (8, 18) }, false, 2, false, new[] { SpikeDirection.Up });
        foreach (int phase in new[] { 0, 1 })
        {
            var s = new PhaseController(new[] { new Block(0, 160, 320, 24) }, 32, 133, platforms: new[] { row }); s.phase = phase;
            s.player.x = row.attached[0].x; s.player.y = row.attached[0].y + 5 - 11 + 4; s.player.vy = 0;
            Assert.That(s.CheckHazards(), Is.EqualTo(phase == 1), $"phase {phase}");
        }
        // A spiked row that slides into a standing player kills him.
        var sweep = new Platform(false, 1, new[] { (6, 19), (14, 19) }, false, 7, false, new[] { SpikeDirection.Right });
        var hit = new PhaseController(new[] { new Block(0, 160, 320, 24) }, 100, 149, platforms: new[] { sweep });
        sweep.queued = 1; Run(hit, 30);
        Assert.That(hit.deaths, Is.EqualTo(1));
    }

    [Test]
    public void SpikeCoveredBlocksKillOnAnyFaceAndBlockLasersOnlyInTheirOwnColor()
    {
        var covered = new Block(100, 140, 8, 20, -1, 2) { spiked = true };
        foreach (var (phase, x, y) in new[] { (1, 92d, 149d), (1, 108d, 149d), (1, 96d, 129d), (1, 96d, 160d) })
        {
            var c = new PhaseController(new[] { new Block(0, 160, 320, 24), covered }, 32, 149); c.phase = phase;
            c.player.x = x; c.player.y = y;
            Assert.That(c.CheckHazards(), Is.True, $"touching the face at {x},{y} in blue kills");
        }
        var graze = new PhaseController(new[] { new Block(0, 160, 320, 24), covered }, 32, 149); graze.phase = 1;
        graze.player.x = 92; graze.player.y = 130;
        Assert.That(graze.CheckHazards(), Is.False, "brushing a corner beside the teeth does not kill");
        var red = new PhaseController(new[] { new Block(0, 160, 320, 24), covered }, 32, 149);
        red.player.x = 92; red.player.y = 149;
        Assert.That((red.CheckHazards(), red.Solid().Contains(covered)), Is.EqualTo((false, false)), "dark spike-covered blocks are harmless and passable");
        red.player.x = 100;
        Assert.That(red.CheckHazards(), Is.False, "even inside it");
        var near = new PhaseController(new[] { new Block(0, 160, 320, 24), covered }, 32, 149); near.phase = 1;
        near.player.x = 90; near.player.y = 149;
        Assert.That(near.CheckHazards(), Is.False, "not touching is safe");
        // A block that turns solid around Wick does not kill him; it waits until he leaves.
        var ghost = new PhaseController(new[] { new Block(0, 160, 320, 24), covered }, 100, 149); ghost.Step(Jump);
        Assert.That((ghost.phase, ghost.deaths, ghost.blocked.Count), Is.EqualTo((1, 0, 1)));
        // Lasers stop at it in blue and pass in red.
        var laser = new Laser(40, 144, LaserDirection.Right, 7);
        var beam = new PhaseController(new[] { covered }, 32, 40, lasers: new[] { laser });
        Assert.That(beam.LaserBeam(laser).w, Is.EqualTo(320 - 48), "dark: the beam runs to the room edge");
        beam.phase = 1; Assert.That(beam.LaserBeam(laser).w, Is.EqualTo(100 - 48), "lit: it ends at the block");
        // A spike-covered platform is a moving spike block.
        var spiked = new Platform(false, 1, new[] { (6, 18), (14, 18) }, false, 7, true);
        var p = new PhaseController(new[] { new Block(0, 160, 320, 24) }, 40, 133, platforms: new[] { spiked });
        Run(p, 6); Assert.That(p.deaths, Is.EqualTo(1), "landing on it kills");
    }

    [Test]
    public void VerticalPlatformsPutTheirSpikeRowsOnTheRightSidesAndReverseCarriesThemAlong()
    {
        var tall = new Platform(true, 3, new[] { (5, 10), (5, 6) }, false, 7, false,
            new[] { SpikeDirection.Up, SpikeDirection.Down, SpikeDirection.Left, SpikeDirection.Right });
        Assert.That((tall.x, tall.y, tall.w, tall.h), Is.EqualTo((40d, 72d, 8d, 24d)));
        Assert.That(tall.attached.Select(s => (s.direction, s.x, s.y, s.w, s.h)), Is.EquivalentTo(new[] {
            (SpikeDirection.Up, 40d, 64d, 8d, 8d), (SpikeDirection.Down, 40d, 96d, 8d, 8d),
            (SpikeDirection.Left, 32d, 72d, 8d, 24d), (SpikeDirection.Right, 48d, 72d, 8d, 24d) }), "left and right rows span the length, up and down one cell");
        var back = new Platform(true, 3, new[] { (5, 10), (5, 6), (9, 6) }, true, 7, false, new[] { SpikeDirection.Up });
        var c = new PhaseController(new[] { new Block(0, 160, 320, 24) }, 200, 149, platforms: new[] { back });
        back.queued = 1; Run(c, 20);
        Assert.That((back.pivot, back.x, back.y), Is.EqualTo((2, 72d, 40d)), "reverse visits the last pivot first");
        Assert.That((back.attached[0].x, back.attached[0].y), Is.EqualTo((72d, 32d)), "the row moved with it");
    }

    [Test]
    public void APlatformPassingThroughThePlayerKeepsItsSpikesHarmless()
    {
        var platform = new Platform(false, 3, new[] { (10, 5), (20, 5) }, false, 7, false, new[] { SpikeDirection.Up });
        var c = Room(platform);
        var row = platform.attached[0]; c.player.x = row.x + 8; c.player.y = row.y + 5 - 6; c.player.vy = 0;
        c.blocked.Add(c.PlatformIndex(0));
        Assert.That(c.CheckHazards(), Is.False, "no spike of a platform in pass-through hurts");
        c.blocked.Clear();
        Assert.That(c.CheckHazards(), Is.True, "and they hurt again once it is solid to Wick");
        var stray = new Spike(row.x, row.y, 24, SpikeDirection.Up) { owner = Mover() };
        var lone = new PhaseController(Array.Empty<Block>(), 32, 40, spikes: new[] { stray });
        lone.player.x = stray.x + 8; lone.player.y = stray.y + 5 - 6;
        Assert.That(lone.CheckHazards(), Is.True, "a row whose platform is not in this room still hurts, without an index error");
    }

    [Test]
    public void APushNeverCarriesThePlayerOutThroughTheRoomBottom()
    {
        double Fall(bool withPlatform)
        {
            var platform = new Platform(false, 1, new[] { (6, 20), (6, 24) });
            var c = withPlatform ? new PhaseController(Array.Empty<Block>(), 48, 169, platforms: new[] { platform }) : new PhaseController(Array.Empty<Block>(), 48, 169);
            platform.queued = 1; bool passed = false;
            for (int i = 0; i < 12; i++) { c.Step(None); passed |= withPlatform && c.blocked.Contains(c.PlatformIndex(0)); }
            Assert.That(c.deaths, Is.Zero);
            if (withPlatform) Assert.That(passed, Is.True, "it passes through instead");
            return c.player.y;
        }
        Assert.That(Fall(true), Is.EqualTo(Fall(false)), "the platform did not move Wick");
    }

    [Test]
    public void GrowingARoomDoesNotStretchAPlatformAgainstTheRightEdge()
    {
        var platform = new Platform(false, 3, new[] { (38, 5), (30, 5) });
        var c = Room(platform);
        Assert.That(platform.x + platform.w, Is.EqualTo(320));
        c.ResizeRoom(400);
        Assert.That((platform.w, c.blocks[0].w), Is.EqualTo((24d, 400d)), "the floor grows, the platform keeps its size");
    }
}
