using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Phase;

public class CrystalRoomTests
{
    static JToken Room(int index) => JObject.Parse(File.ReadAllText("Assets/Phase/CrystalRooms.json"))["rooms"][index];
    static bool Reached(PhaseController s, JToken goal) => s.deaths == 0 && s.player.grounded &&
        s.player.x >= (double)goal["x"] && s.player.y + s.player.h == (double)goal["y"];
    static void Replay(PhaseController s, JToken route, System.Func<bool> stop = null)
    {
        bool lastJump = false, lastDash = false;
        foreach (var step in route)
        {
            bool jump = (bool?)step["jump"] ?? false, dash = (bool?)step["dash"] ?? false;
            for (int n = 0; n < (int)step["frames"]; n++)
            {
                s.Step(new Controls { x = (int?)step["x"] ?? 0, y = (int?)step["y"] ?? 0,
                    grab = (bool?)step["grab"] ?? false,
                    jump = jump && !lastJump, jumpHeld = jump,
                    dash = dash && !lastDash, dashHeld = dash });
                lastJump = jump; lastDash = dash;
                if (stop != null && stop()) return;
            }
        }
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void AuthoredCrystalRouteReachesItsLandingWithoutDeaths(int index)
    {
        var room = Room(index); var s = room.ToObject<PhaseDemoRoom>().CreateController();
        Replay(s, room["route"]); var goal = room["goal"];
        Assert.That(Reached(s, goal), Is.True, $"{room["name"]}: route must land without respawning");
        Assert.That(s.phase, Is.EqualTo((int)goal["phase"]));
        Assert.That(s.jumps, Is.EqualTo((int)goal["jumps"]));
        Assert.That(s.dashes, Is.EqualTo((int)goal["dashes"]));
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void AuthoredRoutesLandBeforeTheirLinkedExit(int index)
    {
        var room = Room(index); var goal = room["goal"];
        var s = room.ToObject<PhaseDemoRoom>().CreateController(); s.exitRight = true;
        bool landed = false;
        Replay(s, room["route"], () =>
        {
            landed = Reached(s, goal) && s.phase == (int)goal["phase"] &&
                s.jumps == (int)goal["jumps"] && s.dashes == (int)goal["dashes"];
            return landed || s.ExitDirection != 0;
        });
        Assert.That(landed, Is.True, $"{room["name"]}: authored landing must precede room travel");
        Assert.That(s.ExitDirection, Is.Zero);
    }
    [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(1, 1)] [TestCase(2, 0)] [TestCase(2, 1)]
    public void RemovingAnyRequiredCrystalBreaksItsAuthoredRoute(int index, int crystal)
    {
        var room = Room(index); var s = room.ToObject<PhaseDemoRoom>().CreateController();
        s.crystals.RemoveAt(crystal); Replay(s, room["route"]);
        Assert.That(Reached(s, room["goal"]), Is.False);
    }
    [TestCase(0, 3)] [TestCase(0, 5)] [TestCase(1, 5)] [TestCase(1, 10)] [TestCase(2, 4)] [TestCase(2, 8)]
    public void AirActionTimingAllowsFourFramesEarlierOrLater(int index, int action)
    {
        var room = Room(index);
        for (int delta = -4; delta <= 4; delta++)
        {
            var route = (JArray)room["route"].DeepClone();
            route[action - 2]["frames"] = (int)route[action - 2]["frames"] + delta;
            route[action]["frames"] = (int)route[action]["frames"] - delta;
            var s = room.ToObject<PhaseDemoRoom>().CreateController(); Replay(s, route);
            Assert.That(Reached(s, room["goal"]), Is.True, $"{room["name"]}: air action shifted {delta} frames");
            Assert.That(s.phase, Is.EqualTo((int)room["goal"]["phase"]));
            Assert.That(s.jumps, Is.EqualTo((int)room["goal"]["jumps"]));
            Assert.That(s.dashes, Is.EqualTo((int)room["goal"]["dashes"]));
        }
    }
#if UNITY_EDITOR
    [Test]
    public void UnitySceneDataPreservesColoredCrystalKindsAndMasks()
    {
        string source = File.ReadAllText("Assets/Phase/CrystalRooms.json");
        var unity = UnityEngine.JsonUtility.FromJson<PhaseDemoRooms>(source);
        Assert.That(unity.rooms.Length, Is.EqualTo(3));
        for (int i = 0; i < unity.rooms.Length; i++)
        {
            var expected = Room(i).ToObject<PhaseDemoRoom>().CreateController();
            var actual = unity.rooms[i].CreateController();
            CollectionAssert.AreEqual(expected.crystals.Select(c => (c.x, c.y, c.kind, c.Mask)),
                actual.crystals.Select(c => (c.x, c.y, c.kind, c.Mask)));
        }
    }
#endif
    [Test]
    public void FastDashJumpsCannotSkipTheRelayCrystalsOrLeaveTheBlueIslandByJumping()
    {
        var room = Room(2); var s = room.ToObject<PhaseDemoRoom>().CreateController();
        foreach (var route in room["bypassRoutes"])
        {
            s.Reset(); s.deaths = 0; Replay(s, route, () => s.deaths > 0);
            Assert.That(s.deaths, Is.GreaterThan(0), $"{room["name"]}: recorded shortcut must hit a laser");
        }
    }
    [TestCase(0)] [TestCase(2)]
    public void JumpingInsteadOfTheFinalDashHitsALaser(int index)
    {
        var room = Room(index); var route = (JArray)room["route"].DeepClone();
        var last = route.Last(step => (bool?)step["dash"] == true);
        last["dash"] = false; last["jump"] = true;
        var s = room.ToObject<PhaseDemoRoom>().CreateController(); Replay(s, route, () => s.deaths > 0);
        Assert.That(s.deaths, Is.GreaterThan(0));
    }
    [Test]
    public void SkippingTheStartingColorHopHitsTheFirstLaser()
    {
        var room = Room(1); var route = (JArray)room["route"].DeepClone(); route.RemoveAt(0);
        var s = room.ToObject<PhaseDemoRoom>().CreateController(); Replay(s, route, () => s.deaths > 0);
        Assert.That(s.deaths, Is.GreaterThan(0));
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void PermanentArrivalLandingsAtBothEdgesStayClearOfLasers(int index)
    {
        var room = Room(index); var s = room.ToObject<PhaseDemoRoom>().CreateController();
        var goal = room["goal"];
        foreach (var (x, feet) in new[] { (0.0, (double)room["spawnY"] + 11), (312.0, (double)goal["y"]) })
            for (int phase = 0; phase < 3; phase++)
            {
                s.Reset(); s.phase = phase; s.player.x = x; s.player.y = feet - s.player.h;
                for (int n = 0; n < 30; n++) s.Step(default);
                Assert.That(s.deaths, Is.Zero, $"{room["name"]}: arrival at x={x} in phase {phase}");
                Assert.That(s.player.grounded && s.player.y + s.player.h == feet, Is.True);
            }
        Assert.That(s.blocks.Any(b => b.x + b.w == 320 && b.y == (double)goal["y"] && b.Mask == 7), Is.True);
    }
    [Test]
    public void RoomsMixRoundAndSquareCrystalsWithContinuousLasers()
    {
        var kinds = Enumerable.Range(0, 3).Select(i => Room(i).ToObject<PhaseDemoRoom>().CreateController()).ToArray();
        CollectionAssert.AreEqual(new[] { 1, 2, 2 }, kinds.Select(s => s.crystals.Count));
        Assert.That(kinds[2].crystals.Select(c => c.kind), Is.EqualTo(new[] { CrystalKind.Dash, CrystalKind.Jump }));
        Assert.That(kinds.All(s => s.lasers.Count >= 3 && s.blocks.All(b => b.Mask >= 1 && b.Mask <= 7)), Is.True);
    }
}
