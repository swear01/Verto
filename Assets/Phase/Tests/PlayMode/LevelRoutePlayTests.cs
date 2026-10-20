using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Phase;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class LevelRoutePlayTests : InputTestFixture
{
    [UnityTest]
    public IEnumerator KeyboardRouteReachesTheRightExitWithoutDeaths([ValueSource(typeof(PhaseSandbox), nameof(PhaseSandbox.Levels))] string level)
    {
        if (PhaseRoute.UnroutedReason(level) is string unrouted) Assert.Ignore($"{level}: {unrouted}");
        yield return SceneManager.LoadSceneAsync(level);
        var k = InputSystem.AddDevice<Keyboard>();
        float captureDelta = Time.captureDeltaTime;
        try
        {
            var sandbox = Object.FindAnyObjectByType<PhaseSandbox>();
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (sandbox.InTransition && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Time.captureDeltaTime = (float)PhaseController.Dt;
            InputSystem.QueueStateEvent(k, new KeyboardState()); yield return null;
            sandbox.Controller.Reset();
            int deaths = sandbox.Controller.deaths;
            bool Done() => sandbox.Won || sandbox.InTransition || sandbox.Controller.deaths != deaths;
            foreach (var step in PhaseRoute.Parse(File.ReadAllText(PhaseRoute.RoutePath(level))))
            {
                var keys = new List<Key>();
                if (step.x != 0) keys.Add(step.x < 0 ? Key.LeftArrow : Key.RightArrow);
                if (step.y != 0) keys.Add(step.y < 0 ? Key.UpArrow : Key.DownArrow);
                if (step.jump) keys.Add(Key.Z); if (step.dash) keys.Add(Key.X); if (step.grab) keys.Add(Key.C);
                InputSystem.QueueStateEvent(k, new KeyboardState(keys.ToArray()));
                for (int n = 0; n < step.frames && !Done(); n++) yield return null;
                if (Done()) break;
            }
            InputSystem.QueueStateEvent(k, new KeyboardState(Key.RightArrow));
            deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!Done() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(sandbox.Controller.deaths, Is.EqualTo(deaths), $"{level}: keyboard route has no deaths");
            int index = System.Array.IndexOf(PhaseSandbox.Levels, level);
            if (index == PhaseSandbox.Levels.Length - 1) { Assert.That(sandbox.Won, Is.True, $"{level}: right edge wins"); yield break; }
            Assert.That(sandbox.InTransition, Is.True, $"{level}: route crosses the right edge");
            string next = PhaseSandbox.Levels[index + 1];
            deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (SceneManager.GetActiveScene().name != next && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(next));
        }
        finally { Time.captureDeltaTime = captureDelta; InputSystem.QueueStateEvent(k, new KeyboardState()); }
    }
}
