using System.IO;
using System.Linq;
using NUnit.Framework;
using Phase;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class LevelRouteTests
{
    // Replays a level's recorded keyboard route on the controller its saved scene builds.
    [TestCase("PhaseDemo13")]
    public void RecordedRouteLeavesThroughTheRightEdgeWithoutDying(string level)
    {
        var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{level}.unity", OpenSceneMode.Additive);
        try
        {
            var room = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<PhaseSandbox>()).Single().CreateController();
            var steps = PhaseRoute.Parse(File.ReadAllText(PhaseRoute.RoutePath(level)));
            Assert.That(PhaseRoute.Replay(room, steps, holdRight: 0), Is.EqualTo(1), $"{level} route reaches the right exit");
            Assert.That(room.deaths, Is.Zero);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
}
