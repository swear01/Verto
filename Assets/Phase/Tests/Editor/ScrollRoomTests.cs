using System;
using NUnit.Framework;
using Phase;

public class ScrollRoomTests
{
    static PhaseController Big(params Block[] blocks) => new PhaseController(blocks, 32, 149, width: 640, height: 360);

    [Test]
    public void RoomSizeDefaultsToOneScreenAndValidatesGridSteps()
    {
        var c = new PhaseController(Array.Empty<Block>(), 32, 149);
        Assert.That((c.RoomWidth, c.Width, c.Height), Is.EqualTo((320, 320, 180)));
        Assert.That((Big().Width, Big().Height), Is.EqualTo((640, 360)));
        Assert.That(new PhaseController(Array.Empty<Block>(), 32, 149, width: 328, height: 184).Height, Is.EqualTo(184));
        foreach (var (w, h) in new[] { (312, 180), (324, 180), (320, 168), (320, 182), (640, 362) })
            Assert.Throws<ArgumentOutOfRangeException>(() => new PhaseController(Array.Empty<Block>(), 32, 149, width: w, height: h), $"{w}x{h}");
    }

    [Test]
    public void DeathLineSitsFourteenPixelsBelowTheRoomHeight()
    {
        var small = new PhaseController(Array.Empty<Block>(), 32, 200);
        small.Step(default); Assert.That(small.deaths, Is.EqualTo(1), "default rooms keep the 194 death line");
        var big = Big(); big.player.y = 200; big.Step(default);
        Assert.That(big.deaths, Is.Zero, "a tall room is still open below one screen");
        big.player.y = 374; big.player.vy = 0; big.Step(default); Assert.That(big.deaths, Is.Zero);
        big.player.y = 375; big.Step(default); Assert.That(big.deaths, Is.EqualTo(1));
    }

    [Test]
    public void SideExitsAndWallsUseTheRoomEdge()
    {
        var c = Big(new Block(0, 352, 640, 8)); var p = c.player; p.y = 341;
        p.x = 630; p.vx = 90; Ticks(c, 30, new Controls { x = 1 });
        Assert.That(p.x, Is.EqualTo(632), "closed right side clamps at the room edge");
        c.exitRight = true; Ticks(c, 10, new Controls { x = 1 });
        Assert.That(c.ExitDirection, Is.EqualTo(1));
        c.ResizeRoom(427); Assert.That((c.Width, c.blocks[0].w), Is.EqualTo((640, 640.0)), "a narrower view does not shrink a big room");
        c.ResizeRoom(700); Assert.That((c.Width, c.blocks[0].w), Is.EqualTo((700, 700.0)), "a wider view still extends edge terrain");
    }

    [Test]
    public void LasersRunToTheRoomEdges()
    {
        var down = new Laser(600, 300, LaserDirection.Down); var right = new Laser(8, 300, LaserDirection.Right);
        var c = new PhaseController(Array.Empty<Block>(), 32, 149, lasers: new[] { down, right }, width: 640, height: 360);
        var a = c.LaserBeam(down); var b = c.LaserBeam(right);
        Assert.That(a.y + a.h, Is.EqualTo(360)); Assert.That(b.x + b.w, Is.EqualTo(640));
        Assert.That(new Laser(120, 80, LaserDirection.Down).Beam(Array.Empty<Block>(), 320).h, Is.EqualTo(92), "default height stays 180");
        c.player.x = 400; c.player.y = 298; Assert.That(c.CheckHazards(), Is.True, "beam beyond the first screen is lethal");
    }

    [Test]
    public void TransitionsBetweenRoomSizesKeepHeightInsideTheNewRoom()
    {
        var big = Big(); big.player.x = 633; big.player.y = 300; big.phase = 2;
        var small = new PhaseController(new[] { new Block(0, 160, 320, 24) }, 32, 149);
        small.EnterFrom(big, 1);
        Assert.That((small.player.x, small.player.y, small.phase), Is.EqualTo((0.0, 169.0, 2)), "clamped to the shorter room");
        var back = Big(); small.player.y = 100; back.EnterFrom(small, -1);
        Assert.That((back.player.x, back.player.y), Is.EqualTo((632.0, 100.0)), "arrives at the far edge at the same height");
        var same = new PhaseController(Array.Empty<Block>(), 32, 149); var from = new PhaseController(Array.Empty<Block>(), 32, 149);
        from.player.y = 175; same.EnterFrom(from, 1); Assert.That(same.player.y, Is.EqualTo(175), "equal heights keep today's falling arrival");
    }

    [Test]
    public void CameraFollowsThePlayerAndNeverLeavesTheRoom()
    {
        var c = Big(); var view = new PhaseCamera(); c.player.y = 341;
        view.Follow(c, 320, 180, true); Assert.That((view.X, view.Y), Is.EqualTo((0, 180)), "clamped at the bottom-left");
        c.player.x = 316; c.player.y = 180; view.Follow(c, 320, 180, true);
        Assert.That((view.X, view.Y), Is.EqualTo((160, 96)), "centred on the player");
        c.player.x = 632; c.player.y = 0;
        view.Follow(c, 320, 180); Assert.That(view.X, Is.InRange(161, 200), "eases instead of jumping");
        for (int i = 0; i < 600; i++) { view.Follow(c, 320, 180); Assert.That(view.x, Is.InRange(0, 320)); Assert.That(view.y, Is.InRange(0, 180)); }
        Assert.That((view.X, view.Y), Is.EqualTo((320, 0)), "settles at the top-right corner");
        view.Follow(c, 640, 400, true); Assert.That((view.X, view.Y), Is.EqualTo((0, -40)), "a view taller than the room sits the room at the bottom");

        var room = new PhaseController(Array.Empty<Block>(), 300, 10);
        foreach (var (w, h, y) in new[] { (320, 180, 0), (320, 693, -513), (427, 180, 0) })
        {
            room.ResizeRoom(w); view.Follow(room, w, h); Assert.That((view.X, view.Y), Is.EqualTo((0, y)), $"default room in {w}x{h}");
        }
    }

    static void Ticks(PhaseController s, int count, Controls input = default)
    {
        for (int i = 0; i < count; i++) s.Step(input);
    }
}
