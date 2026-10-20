using System.IO;
using System.Linq;
using NUnit.Framework;
using Phase;
using UnityEditor.SceneManagement;

public class LevelRouteTests
{
    [TestCaseSource(typeof(PhaseSandbox), nameof(PhaseSandbox.Levels))]
    public void RecordedRouteClearsAndBypassesStayBlocked(string level)
    {
        if (PhaseRoute.UnroutedReason(level) is string unrouted) Assert.Ignore($"{level}: {unrouted}");
        var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{level}.unity", OpenSceneMode.Additive);
        PhaseController c;
        try { c = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<PhaseSandbox>()).Single().CreateController(); }
        finally { EditorSceneManager.CloseScene(scene, true); }
        Assert.That(PhaseRoute.Replay(c, PhaseRoute.Parse(File.ReadAllText(PhaseRoute.RoutePath(level)))), Is.EqualTo(1), $"{level}: route reaches the right edge");
        Assert.That(c.deaths, Is.Zero, $"{level}: route has no deaths");
        if (!File.Exists(PhaseRoute.BypassPath(level))) return;
        foreach (var bypass in PhaseRoute.ParseBypasses(File.ReadAllText(PhaseRoute.BypassPath(level))))
        {
            c.Reset(); c.deaths = 0;
            Assert.That(PhaseRoute.Replay(c, bypass.steps, 0), Is.Not.EqualTo(1), $"{level}: {bypass.name} must not reach the exit");
        }
    }
}
