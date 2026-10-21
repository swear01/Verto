using System;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Phase;

public class WorldTests
{
    static readonly Controls Right = new Controls { x = 1 }, Left = new Controls { x = -1 };

    static PhaseDemoRoom Room(int x, int y, int w = 1, int h = 1, bool floor = true, params Block[] more)
    {
        int width = w * 320, height = h * 176;
        return new PhaseDemoRoom {
            atX = x, atY = y, width = width, height = height, spawnX = 24, spawnY = height - 24,
            blocks = (floor ? new[] { new Block(0, height - 8, width, 8, phaseMask: 7) } : new[] { new Block(0, 0, 8, 8, phaseMask: 7) }).Concat(more).ToArray()
        };
    }
    static PhaseWorld World(params PhaseDemoRoom[] rooms) => new PhaseWorld { rooms = rooms };
    // Steps until the room changes; fails if it never does.
    static void Cross(PhaseWorldPlay play, Controls input, int limit = 400)
    {
        int room = play.Room;
        for (int i = 0; i < limit && play.Room == room; i++) play.Step(input);
        Assert.That(play.Room, Is.Not.EqualTo(room), "the room changed");
    }
    // The player's position in world pixels.
    static (double x, double y) At(PhaseWorldPlay play)
    {
        var room = play.world.rooms[play.Room]; var p = play.Controller.player;
        return (p.x + room.atX * 320, p.y + room.atY * 176);
    }
    static void Carry(PhaseController c) { c.phase = 1; c.jumps = 5; c.deaths = 2; c.player.airJump = 1; c.time = 3; }
    static void AssertCarried(PhaseWorldPlay play, PhaseController from, (double x, double y) before, double vx, double vy)
    {
        var c = play.Controller;
        Assert.That((c.phase, c.jumps, c.deaths, c.player.airJump), Is.EqualTo((1, 5, 2, 1)));
        Assert.That(c.time, Is.EqualTo(from.time));
        Assert.That(c.player.vx, Is.EqualTo(vx).Within(1e-9)); Assert.That(c.player.vy, Is.EqualTo(vy).Within(1e-9));
        var now = At(play);
        Assert.That(Math.Abs(now.x - before.x), Is.LessThanOrEqualTo(2)); Assert.That(Math.Abs(now.y - before.y), Is.LessThanOrEqualTo(2), "the position is translated by the room offset");
        Assert.That(play.pause, Is.EqualTo(PhaseWorldPlay.Pause));
    }

    [Test]
    public void CrossingASideCarriesTheStateAndPausesPlay()
    {
        foreach (int side in new[] { 1, -1 })
        {
            var play = new PhaseWorldPlay(World(Room(0, 0), Room(side, 0)), 0);
            var from = play.Controller; Carry(from);
            from.player.x = side > 0 ? 300 : 12; from.player.y = 157;
            for (int i = 0; i < 400 && play.Room == 0; i++) { play.Step(side > 0 ? Right : Left); }
            Assert.That(play.Room, Is.EqualTo(1), "side " + side);
            AssertCarried(play, from, At(play), play.Controller.player.vx, play.Controller.player.vy);
            Assert.That(Math.Sign(play.Controller.player.vx), Is.EqualTo(side));
            Assert.That(play.Controller.player.x, Is.EqualTo(side > 0 ? -4 : 316).Within(2), "the player is carried across the seam");
            Assert.That(play.slideX, Is.EqualTo(side));
        }
    }

    [Test]
    public void CrossingUpAndDownCarriesTheStateAndSlidesVertically()
    {
        var up = new PhaseWorldPlay(World(Room(0, 0), Room(0, -1)), 0);
        var from = up.Controller; Carry(from); from.player.y = 8; from.player.vy = -200; from.player.grounded = false;
        var before = At(up); Cross(up, default);
        Assert.That((up.Room, up.slideX, up.slideY), Is.EqualTo((1, 0, -1)));
        Assert.That(up.Controller.player.y, Is.LessThan(176).And.GreaterThan(160), "arrives at the bottom of the room above");
        Assert.That(up.Controller.player.vy, Is.LessThan(0));
        Assert.That((up.Controller.phase, up.Controller.jumps, up.Controller.deaths, up.Controller.player.airJump), Is.EqualTo((1, 5, 2, 1)));
        Assert.That(At(up).y - before.y, Is.LessThan(0).And.GreaterThan(-20), "moved up by a few pixels, not by a room");

        var down = new PhaseWorldPlay(World(Room(0, 0, floor: false), Room(0, 1)), 0);
        from = down.Controller; Carry(from); from.player.y = 150; from.player.vy = 100; from.player.grounded = false;
        before = At(down); Cross(down, default);
        Assert.That((down.Room, down.slideX, down.slideY), Is.EqualTo((1, 0, 1)));
        Assert.That(down.Controller.player.y, Is.LessThan(16), "arrives at the top of the room below");
        Assert.That((down.Controller.phase, down.Controller.jumps, down.Controller.deaths, down.Controller.player.airJump), Is.EqualTo((1, 5, 2, 1)));
        Assert.That(At(down).y - before.y, Is.GreaterThan(0).And.LessThan(40));
        Assert.That(down.Controller.deaths, Is.EqualTo(2), "falling into a neighbor is not a death");
    }

    [Test]
    public void TheTransitionPausesForAFifthOfASecondAndKeepsTimeStill()
    {
        var play = new PhaseWorldPlay(World(Room(0, 0), Room(1, 0)), 0);
        play.Controller.player.x = 310; Cross(play, Right);
        var c = play.Controller; double time = c.time; int steps = 0;
        for (; play.pause > 0; steps++) Assert.That(play.Step(new Controls { jump = true }), Is.False);
        Assert.That(steps, Is.EqualTo(11), "0.18 s of 1/60 s steps");
        Assert.That(c.time, Is.EqualTo(time), "the pause freezes the room");
        Assert.That(c.jumps, Is.Zero, "the held jump was not spent during the pause");
    }

    [Test]
    public void ASideWithoutANeighborIsAnInvisibleWallAtAnyHeight()
    {
        var play = new PhaseWorldPlay(World(Room(0, 0)));
        var c = play.Controller; c.player.x = 3; c.player.vx = -90;
        for (int i = 0; i < 30; i++) play.Step(Left);
        Assert.That(c.player.x, Is.EqualTo(0));
        c.player.x = 3; c.player.y = -60; c.player.vy = 0; c.player.vx = -90;
        for (int i = 0; i < 6; i++) play.Step(Left);
        Assert.That(c.player.x, Is.EqualTo(0), "also above the top");
        c.player.x = 305; c.player.vx = 90; c.player.y = -60;
        for (int i = 0; i < 20; i++) play.Step(Right);
        Assert.That(c.player.x, Is.EqualTo(312), "and on the right");
        Assert.That(c.deaths, Is.Zero);
    }

    [Test]
    public void ASideIsOpenOnlyAlongTheNeighbor()
    {
        // A is two screens tall; B touches only its lower screen.
        var a = Room(0, 0, 1, 2); var b = Room(1, 1);
        var play = new PhaseWorldPlay(World(a, b), 0);
        var c = play.Controller;
        c.player.x = 306; c.player.y = 40; c.player.vy = 0; c.player.grounded = true;
        for (int i = 0; i < 20; i++) { c.player.y = 40; play.Step(Right); }
        Assert.That((play.Room, c.player.x), Is.EqualTo((0, 312.0)), "the upper half of the side is a wall");
        c.player.y = 250; c.player.x = 306;
        for (int i = 0; i < 20 && play.Room == 0; i++) { c.player.y = 250; play.Step(Right); }
        Assert.That(play.Room, Is.EqualTo(1), "the lower half opens onto B");
    }

    [Test]
    public void TheTopIsOpenAirAndTheBottomKillsUnlessANeighborIsBelow()
    {
        var play = new PhaseWorldPlay(World(Room(0, 0)));
        var c = play.Controller; c.player.x = 100; c.player.y = 10; c.player.vy = -330; c.player.grounded = false;
        double top = 0;
        for (int i = 0; i < 240; i++) { play.Step(default); top = Math.Min(top, c.player.y); }
        Assert.That(top, Is.LessThan(-40), "the player rises above the frame");
        Assert.That((c.deaths, c.player.grounded), Is.EqualTo((0, true)), "and lands again without dying");
        Assert.That(c.player.y, Is.EqualTo(157));

        var pit = new PhaseWorldPlay(World(Room(0, 0, floor: false)));
        pit.Controller.player.y = 150; pit.Controller.player.vy = 300;
        for (int i = 0; i < 25; i++) pit.Step(default);
        Assert.That(pit.Controller.deaths, Is.EqualTo(1), "no neighbor below: death past Height + 14");

        // B hangs below only the left half of a two-screen-wide A.
        var wide = new PhaseWorldPlay(World(Room(0, 0, 2, 1, false), Room(0, 1)), 0);
        var d = wide.Controller; d.player.x = 500; d.player.y = 150; d.player.vy = 200;
        for (int i = 0; i < 25; i++) wide.Step(default);
        Assert.That((wide.Room, d.deaths), Is.EqualTo((0, 1)), "the right half has nothing below");
        var left = new PhaseWorldPlay(World(Room(0, 0, 2, 1, false), Room(0, 1)), 0);
        left.Controller.player.x = 100; left.Controller.player.y = 150; left.Controller.player.vy = 200;
        Cross(left, default);
        Assert.That((left.Room, left.Controller.deaths), Is.EqualTo((1, 0)));
    }

    [Test]
    public void TheRespawnSkipsASpawnUnderALaserOfTheEntryPhase()
    {
        var b = Room(1, 0); b.extraSpawns = new[] { new DemoPoint { x = 280, y = 152 } };
        b.lasers = new[] { new DemoLaser { x = 24, y = 40, direction = LaserDirection.Down, phaseMask = 2 } };
        var play = new PhaseWorldPlay(World(Room(0, 0), b), 0);
        var c = play.Controller; c.phase = 1; c.player.x = 310; c.player.y = 157;
        Cross(play, Right);
        Assert.That((play.Controller.spawnX, play.Controller.respawnPhase), Is.EqualTo((280d, 1)), "the nearest spawn is under the blue laser");
        var red = new PhaseWorldPlay(World(Room(0, 0), b), 0);
        red.Controller.phase = 0; red.Controller.player.x = 310; red.Controller.player.y = 157;
        Cross(red, Right);
        Assert.That((red.Controller.spawnX, red.Controller.respawnPhase), Is.EqualTo((24d, 0)), "the blue laser is off in red");
    }

    [Test]
    public void TheRespawnIsTheNearestSpawnWithTheEntryPhase()
    {
        var b = Room(1, 0); b.extraSpawns = new[] { new DemoPoint { x = 280, y = 152 } };
        var u = Room(1, -1);
        var play = new PhaseWorldPlay(World(Room(0, 0), b, u), 0);
        var c = play.Controller; c.phase = 2; c.player.x = 310; c.player.y = 157;
        Cross(play, Right);
        var inB = play.Controller;
        Assert.That((inB.spawnX, inB.spawnY, inB.respawnPhase), Is.EqualTo((24d, 152d, 2)), "entering at the left edge: the left spawn");
        inB.player.x = 100; inB.phase = 0; inB.player.grounded = true;
        // Dying returns to that spawn in the entry phase.
        inB.player.y = 300; for (int i = 0; i < 40; i++) play.Step(default);
        Assert.That((inB.deaths, inB.player.x, inB.phase), Is.EqualTo((1, 24d, 2)));
        Assert.That(inB.player.y, Is.InRange(150d, 157d), "back at the spawn");

        // Entering from above, at the right: the right spawn.
        var from = new PhaseWorldPlay(World(Room(0, 0), b, u), 2);
        var top = from.Controller; top.phase = 1; top.player.x = 270; top.player.y = 160; top.player.vy = 100; top.player.grounded = false;
        top.blocks.Clear();
        Cross(from, default);
        Assert.That((from.Room, from.Controller.spawnX, from.Controller.spawnY, from.Controller.respawnPhase), Is.EqualTo((1, 280d, 152d, 1)));
    }

    [Test]
    public void EnteringARoomResetsItsPlatformsAndCrystals()
    {
        var b = Room(1, 0);
        b.platforms = new[] { new DemoPlatform { length = 3, pivots = new[] { (20, 10), (30, 10) }, spikes = new SpikeDirection[0] } };
        b.crystals = new[] { new DemoCrystal { x = 200, y = 80, phaseMask = 7 } };
        var play = new PhaseWorldPlay(World(Room(0, 0), b), 0);
        play.Controller.player.x = 310; Cross(play, Right);
        var inB = play.Controller; var platform = inB.platforms[0];
        platform.Move(40, 0); platform.pivot = 1; inB.crystals[0].collected = true;
        inB.player.x = -2; inB.player.vx = -90; Cross(play, Left);
        play.Controller.player.x = 310; Cross(play, Right);
        Assert.That((platform.x, platform.pivot, inB.crystals[0].collected), Is.EqualTo((152d, 0, false)));
    }

    [Test]
    public void TouchingTheGoalWinsInEveryColor()
    {
        var a = Room(0, 0); a.goal = new DemoPoint { x = 160, y = 152 };
        foreach (int phase in new[] { 0, 1, 2 })
        {
            var play = new PhaseWorldPlay(World(a, Room(1, 0)), 0);
            var c = play.Controller; c.phase = phase;
            Assert.That(play.Won, Is.False);
            c.player.x = 150; c.player.y = 141; play.Step(Right);
            Assert.That(play.Won, Is.False, "not there yet");
            for (int i = 0; i < 20 && !play.Won; i++) play.Step(Right);
            Assert.That(play.Won, Is.True, "phase " + phase);
        }
    }

    [Test]
    public void TheCameraStaysInTheRoomBottomAlignedAndCentred()
    {
        Assert.That(PhaseCamera.Clamp(0, 176, 180, true), Is.EqualTo(-4), "a short room sits at the bottom with sky above");
        Assert.That(PhaseCamera.Clamp(0, 180, 693, true), Is.EqualTo(-513));
        Assert.That(PhaseCamera.Clamp(0, 320, 427, false), Is.EqualTo(-53), "a narrow room is centred");
        Assert.That(PhaseCamera.Clamp(999, 640, 320, false), Is.EqualTo(320), "a wide room stays inside");
        Assert.That(PhaseCamera.Clamp(-9, 880, 180, true), Is.EqualTo(0));
        var tall = World(Room(0, 0, 1, 5)).Create(0); var view = new PhaseCamera();
        tall.player.y = 20; view.Follow(tall, 320, 180, true); Assert.That(view.Y, Is.Zero, "the top is not followed past the room");
        tall.player.y = -300; view.Follow(tall, 320, 180, true); Assert.That(view.Y, Is.Zero, "nor above the frame");
        tall.player.y = 860; view.Follow(tall, 320, 180, true); Assert.That(view.Y, Is.EqualTo(700));
        var one = World(Room(0, 0)).Create(0); view.Follow(one, 427, 180, true);
        Assert.That((view.X, view.Y), Is.EqualTo((-53, -4)));
    }

    [Test]
    public void TheWorldStartsInTheFirstRoomByPositionAtItsLeftmostSpawn()
    {
        var first = Room(-1, 2); first.extraSpawns = new[] { new DemoPoint { x = 8, y = 152 } };
        var world = World(Room(3, 0), Room(0, 0), first);
        Assert.That(world.Start, Is.EqualTo(2), "left to right, then top to bottom");
        var play = new PhaseWorldPlay(world); var c = play.Controller;
        Assert.That((play.Room, c.player.x, c.player.y, c.phase), Is.EqualTo((2, 8d, 152d, 0)));
        Assert.That(world.Create(1).neighbors.Select(n => n.room), Is.Empty, "rooms that only touch at a corner or not at all are not neighbors");
        var touching = World(Room(0, 0), Room(1, 0), Room(0, -1), Room(1, 1));
        Assert.That(touching.Create(0).neighbors.Select(n => (n.room, n.x, n.y)), Is.EquivalentTo(new[] { (1, 320d, 0d), (2, 0d, -176d) }));
    }

    [Test]
    public void WorldsValidateAndRoundTrip()
    {
        var a = Room(0, 0, 2, 1); a.goal = new DemoPoint { x = 160, y = 152 };
        var b = Room(-1, -2, 1, 2); b.extraSpawns = new[] { new DemoPoint { x = 200, y = 328 } };
        b.platforms = new[] { new DemoPlatform { length = 3, pivots = new[] { (10, 30), (20, 30) }, spikes = new[] { SpikeDirection.Up } } };
        var world = World(a, b); world.name = "Rooms";
        PhaseCustomMap.Validate(world);
        string json = PhaseCustomMap.ToJson(world);
        var data = JObject.Parse(json);
        Assert.That(((int)data["version"], (string)data["generator"]), Is.EqualTo((3, "Verto 0.3")));
        Assert.That(data.Properties().Select(p => p.Name), Is.EqualTo(new[] { "format", "version", "generator", "name", "rooms" }));
        Assert.That(data["rooms"][0]["at"].ToObject<int[]>(), Is.EqualTo(new[] { 0, 0 }));
        Assert.That(data["rooms"][1]["at"].ToObject<int[]>(), Is.EqualTo(new[] { -1, -2 }));
        Assert.That(data["rooms"][1]["size"].ToObject<int[]>(), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(data["rooms"][1]["objects"].Where(o => (string)o["type"] == "spawn").Count(), Is.EqualTo(2));
        Assert.That(data["rooms"][0]["objects"].Single(o => (string)o["type"] == "goal")["cell"].ToObject<int[]>(), Is.EqualTo(new[] { 20, 19 }));
        var parsed = PhaseCustomMap.Parse(json);
        Assert.That(PhaseCustomMap.ToJson(parsed), Is.EqualTo(json));
        Assert.That((parsed.name, parsed.rooms[1].extraSpawns.Length, parsed.rooms[0].goal.x, parsed.rooms[1].platforms.Length), Is.EqualTo(("Rooms", 1, 160d, 1)));
    }

    [Test]
    public void InvalidWorldsAreRefused()
    {
        void Refuses(Action<PhaseWorld> mutate, string why)
        {
            var world = World(Room(0, 0, 2, 1), Room(2, 0), Room(0, 1));
            mutate(world); Assert.Throws<ArgumentException>(() => PhaseCustomMap.Validate(world), why);
        }
        Assert.DoesNotThrow(() => PhaseCustomMap.Validate(World(Room(0, 0, 2, 1), Room(2, 0), Room(0, 1))), "touching rooms are fine");
        Refuses(w => w.rooms[1].atX = 1, "rooms may not overlap");
        Refuses(w => w.rooms[2].atY = 0, "not even partly");
        Refuses(w => { w.rooms[0].goal = new DemoPoint { x = 8, y = 8 }; w.rooms[1].goal = new DemoPoint { x = 8, y = 8 }; }, "one goal at most");
        Refuses(w => w.rooms[1].width = 480, "whole screens wide");
        Refuses(w => w.rooms[1].height = 200, "whole screens tall");
        Refuses(w => { w.rooms[1].width = 1600; }, "at most four screens wide");
        Refuses(w => { w.rooms[1].height = 6 * 176; }, "at most five screens tall");
        Refuses(w => w.rooms[1].blocks = w.rooms[1].blocks.Concat(new[] { new Block(320, 0, 8, 8) }).ToArray(), "terrain inside its own room");
        Refuses(w => w.rooms[1].spikes = new[] { new DemoSpike { x = 0, y = 176, length = 8, direction = SpikeDirection.Up, phaseMask = 7 } }, "a spike inside its own room");
        Refuses(w => w.rooms[1].goal = new DemoPoint { x = 320, y = 0 }, "the goal inside its own room");
        Refuses(w => w.rooms[1].extraSpawns = new[] { new DemoPoint { x = 320, y = 0 } }, "every spawn inside its own room");
        Refuses(w => w.rooms[1].extraSpawns = new[] { new DemoPoint { x = 100, y = 160 } }, "a spawn may not sit in red terrain");
        Refuses(w => w.rooms[1].platforms = new[] { new DemoPlatform { length = 1, pivots = new[] { (5, 10), (50, 10) }, spikes = new SpikeDirection[0] } }, "every platform stop inside its own room");
        Refuses(w => w.rooms[1].platforms = new[] { new DemoPlatform { length = 3, pivots = new[] { (5, 10), (5, 0) }, spikes = new[] { SpikeDirection.Up } } }, "with its attached rows");
        Refuses(w => w.rooms[1].blocks = new Block[0], "a room needs terrain");
        Assert.Throws<ArgumentException>(() => PhaseCustomMap.Validate(new PhaseWorld { rooms = new PhaseDemoRoom[0] }));
        var json = PhaseCustomMap.ToJson(World(Room(0, 0)));
        foreach (var mutate in new Action<JObject>[] {
            d => ((JArray)d["rooms"][0]["objects"]).Clear(), d => d["rooms"][0]["size"] = new JArray(0, 1), d => d["rooms"][0]["size"] = new JArray(1, 6),
            d => d["rooms"][0]["at"] = new JArray(1.5, 0), d => d["rooms"] = new JArray(), d => d["size"] = new JArray(1, 1), d => d["rooms"][0]["spawn"] = new JArray(1, 1),
            d => { var objects = (JArray)d["rooms"][0]["objects"]; objects.Add(new JObject { ["type"] = "goal", ["cell"] = new JArray(3, 3) }); objects.Add(new JObject { ["type"] = "goal", ["cell"] = new JArray(5, 3) }); }
        })
        {
            var bad = JObject.Parse(json); mutate(bad);
            Assert.Throws<ArgumentException>(() => PhaseCustomMap.Parse(bad.ToString()));
        }
    }

    static JObject V2(int cols, int rows)
    {
        var terrain = new JArray(Enumerable.Range(0, rows).Select(y => y == rows - 1 ? new string('7', cols) : ""));
        return new JObject {
            ["format"] = "verto-map", ["version"] = 2, ["generator"] = "Verto 0.2", ["name"] = "Old", ["size"] = new JArray(cols, rows), ["spawn"] = new JArray(3, rows - 3),
            ["terrain"] = terrain,
            ["objects"] = new JArray(
                new JObject { ["type"] = "spike", ["cell"] = new JArray(10, rows - 4), ["dir"] = "up", ["mask"] = 7 },
                new JObject { ["type"] = "platform", ["orientation"] = "horizontal", ["length"] = 3, ["pivots"] = new JArray(new JArray(8, 12), new JArray(16, 12)), ["mask"] = 7 })
        };
    }

    [Test]
    public void VersionTwoMapsBecomeOneRoomRoundedUpToWholeScreensAndBottomAligned()
    {
        var world = PhaseCustomMap.Parse(V2(40, 22).ToString());
        var room = world.rooms[0];
        Assert.That((world.rooms.Length, world.name, room.atX, room.atY, room.GridWidth, room.GridHeight), Is.EqualTo((1, "Old", 0, 0, 320, 176)));
        Assert.That((room.spawnX, room.spawnY), Is.EqualTo((24d, 152d)), "the old spawn is a spawn object");

        world = PhaseCustomMap.Parse(V2(70, 30).ToString()); room = world.rooms[0];
        Assert.That((room.GridWidth, room.GridHeight), Is.EqualTo((640, 352)), "70x30 cells need 2x2 screens, extra columns on the right");
        Assert.That(room.blocks.Length, Is.EqualTo(70));
        Assert.That(room.blocks.All(b => b.y == 43 * 8), "the floor stays at the bottom of the room");
        Assert.That((room.spawnX, room.spawnY, room.spikes[0].y, room.platforms[0].pivots[0].y), Is.EqualTo((24d, 41d * 8, 40d * 8, 12 + 14)), "objects keep their distance from the bottom");

        world = PhaseCustomMap.Parse(V2(160, 90).ToString()); room = world.rooms[0];
        Assert.That((room.GridWidth, room.GridHeight, room.blocks.Length), Is.EqualTo((1280, 880, 160)), "the largest old map fits the largest room");
        var data = JObject.Parse(PhaseCustomMap.ToJson(world));
        Assert.That(data["rooms"][0]["size"].ToObject<int[]>(), Is.EqualTo(new[] { 4, 5 }));
        Assert.That((int)data["version"], Is.EqualTo(3));
        foreach (var size in new[] { (39, 22), (161, 22), (40, 21), (40, 91) })
            Assert.Throws<ArgumentException>(() => PhaseCustomMap.Parse(V2(size.Item1, size.Item2).ToString()), size.ToString());
        var future = V2(40, 22); future["version"] = 4;
        Assert.That(Assert.Throws<ArgumentException>(() => PhaseCustomMap.Parse(future.ToString())).Message, Does.Contain("第4版"));
    }
}
