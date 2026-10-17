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
    [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(1, 1)] [TestCase(1, 2)] [TestCase(2, 0)]
    public void RemovingAnyRequiredCrystalBreaksItsAuthoredRoute(int index, int crystal)
    {
        var room = Room(index); var s = room.ToObject<PhaseDemoRoom>().CreateController();
        s.crystals.RemoveAt(crystal); Replay(s, room["route"]);
        Assert.That(Reached(s, room["goal"]), Is.False);
    }
    [TestCase(0, 3)] [TestCase(1, 3)] [TestCase(1, 8)] [TestCase(1, 13)] [TestCase(2, 4)]
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
    public void DenseRoomUsesThreeMatchingRoundCrystalsAndTenContinuousLasers()
    {
        var s = Room(1).ToObject<PhaseDemoRoom>().CreateController();
        Assert.That(s.lasers.Count, Is.EqualTo(10));
        CollectionAssert.AreEqual(new[] { 2, 1, 4 }, s.crystals.Select(c => c.Mask));
        Assert.That(s.crystals.All(c => c.kind == CrystalKind.Jump), Is.True);
        Assert.That(s.blocks.All(b => b.Mask >= 1 && b.Mask <= 7), Is.True);
    }
}
