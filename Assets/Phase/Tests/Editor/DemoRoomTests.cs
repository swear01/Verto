using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Phase;

public class DemoRoomTests
{
#if UNITY_EDITOR
    [Test]
    public void UnitySceneDataPreservesMechanismsAndPhaseMasks()
    {
        string source = File.ReadAllText("Assets/Phase/DemoRooms.json");
        var unity = UnityEngine.JsonUtility.FromJson<PhaseDemoRooms>(source);
        var json = JObject.Parse(source)["rooms"];
        for (int i = 0; i < 8; i++)
        {
            var actual = unity.rooms[i].CreateController(); var expected = json[i].ToObject<PhaseDemoRoom>().CreateController();
            Assert.That(actual.gears.Length,Is.EqualTo(expected.gears.Length));
            Assert.That(actual.mover == null,Is.EqualTo(expected.mover == null));
            Assert.That(actual.spikes.Count,Is.EqualTo(expected.spikes.Count));
            CollectionAssert.AreEqual(expected.lasers.Select(l => (l.x,l.y,l.direction,l.Mask)), actual.lasers.Select(l => (l.x,l.y,l.direction,l.Mask)));
            CollectionAssert.AreEqual(expected.blocks.Select(b => b.Mask),actual.blocks.Select(b => b.Mask));
        }
    }
#endif
    [Test]
    public void ExitLandingsExtendToTheResizedRoomEdge()
    {
        var rooms = JObject.Parse(File.ReadAllText("Assets/Phase/DemoRooms.json"))["rooms"];
        foreach (var room in rooms)
        {
            var s = room.ToObject<PhaseDemoRoom>().CreateController(); var goal = room["goal"];
            s.ResizeRoom(427); s.phase = (int)goal["phase"];
            s.player.x = 419; s.player.y = (double)goal["y"] - s.player.h;
            s.Step(default);
            Assert.That(s.player.grounded, Is.True, $"{room["name"]}: exit landing fills a wider viewport");
            s.ResizeRoom(320);
            Assert.That(s.blocks.Where(b => b.x + b.w == 320).Any(), Is.True);
        }
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
    public void AuthoredRouteReachesItsLandingWithoutDeaths(int index)
    {
        var rooms = JObject.Parse(File.ReadAllText("Assets/Phase/DemoRooms.json"))["rooms"];
        Assert.That(rooms.Count(), Is.EqualTo(8));
        var room = rooms[index]; var s = room.ToObject<PhaseDemoRoom>().CreateController();
        Replay(s,room["route"]);
        Assert.That(s.deaths,Is.Zero,$"{room["name"]}: route must not rely on respawn");
        var goal = room["goal"];
        Assert.That(s.player.x, Is.GreaterThanOrEqualTo((double)goal["x"]));
        Assert.That(s.player.y + s.player.h, Is.EqualTo((double)goal["y"]));
        Assert.That(s.player.grounded, Is.True);
        Assert.That(s.phase, Is.EqualTo((int)goal["phase"]));
        Assert.That(s.jumps, Is.EqualTo((int)goal["jumps"]));
        Assert.That(s.dashes, Is.EqualTo((int)goal["dashes"]));
        if (s.gears.Length > 0) Assert.That(s.gears[0].Lift, Is.EqualTo((double)goal["lift"]));
    }
    static void Replay(PhaseController s, JToken steps)
    {
        bool lastJump = false, lastDash = false;
        foreach (var step in steps)
        {
            bool jump = (bool?)step["jump"] ?? false, dash = (bool?)step["dash"] ?? false;
            for (int n = 0; n < (int)step["frames"]; n++)
            {
                s.Step(new Controls {
                    x = (int?)step["x"] ?? 0, y = (int?)step["y"] ?? 0,
                    grab = (bool?)step["grab"] ?? false,
                    jump = jump && !lastJump, jumpHeld = jump,
                    dash = dash && !lastDash, dashHeld = dash
                });
                lastJump = jump; lastDash = dash;
            }
        }
    }
    [Test]
    public void JumpingInsteadOfDashingActivatesTheSpikeGap()
    {
        var room=JObject.Parse(File.ReadAllText("Assets/Phase/DemoRooms.json"))["rooms"][4];
        var s=room.ToObject<PhaseDemoRoom>().CreateController();var route=(JArray)room["route"].DeepClone();
        var mistake=route.First(step => (bool?)step["dash"] == true);
        mistake["dash"]=false;mistake["jump"]=true;
        Replay(s,route);Assert.That(s.deaths,Is.GreaterThan(0));
    }
    [Test]
    public void RedObstructionStopsTheGearUntilThePlayerPlansTheColor()
    {
        var room=JObject.Parse(File.ReadAllText("Assets/Phase/DemoRooms.json"))["rooms"][6];
        var s=room.ToObject<PhaseDemoRoom>().CreateController();
        Replay(s,new JArray(room["route"][1].DeepClone(),room["route"][2].DeepClone()));
        Assert.That(s.gears[0].Lift,Is.EqualTo(16));Assert.That(s.deaths,Is.Zero);
        s.Reset();Assert.That(s.gears[0].Lift,Is.Zero);
    }
    [Test]
    public void ExtraFerryDashesCanRecoverWithoutRespawning()
    {
        var room=JObject.Parse(File.ReadAllText("Assets/Phase/DemoRooms.json"))["rooms"][5];
        var s=room.ToObject<PhaseDemoRoom>().CreateController();Replay(s,room["recoveryRoute"]);
        Assert.That(s.deaths,Is.Zero);Assert.That(s.player.x,Is.GreaterThanOrEqualTo(296));
        Assert.That(s.player.y+s.player.h,Is.EqualTo(112));Assert.That(s.player.grounded,Is.True);
        Assert.That(s.dashes,Is.EqualTo(4));Assert.That(s.mover.x,Is.EqualTo(152));
    }

}
