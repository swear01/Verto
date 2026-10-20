using System;
using System.Collections.Generic;
using UnityEngine;

namespace Phase
{
    // One held-key span of a recorded keyboard route.
    [Serializable]
    public sealed class PhaseRouteStep
    {
        public int frames, x, y;
        public bool jump, dash, grab;
    }

    [Serializable]
    public sealed class PhaseBypass
    {
        public string name;
        public PhaseRouteStep[] steps;
    }

    public static class PhaseRoute
    {
        [Serializable] sealed class Steps { public PhaseRouteStep[] steps; }
        [Serializable] sealed class Bypasses { public PhaseBypass[] routes; }
        public const string Folder = "Verification/Routes";
        public static string RoutePath(string scene) => $"{Folder}/{scene}.json";
        public static string BypassPath(string scene) => $"{Folder}/{scene}.bypass.json";
        public static PhaseRouteStep[] Parse(string json) => JsonUtility.FromJson<Steps>("{\"steps\":" + json + "}").steps;
        public static PhaseBypass[] ParseBypasses(string json) => JsonUtility.FromJson<Bypasses>("{\"routes\":" + json + "}").routes;

        // Keyboard semantics: jump and dash fire on the first frame their key is held.
        public static IEnumerable<Controls> Inputs(IEnumerable<PhaseRouteStep> steps)
        {
            bool lastJump = false, lastDash = false;
            foreach (var step in steps)
                for (int n = 0; n < step.frames; n++)
                {
                    yield return new Controls { x = step.x, y = step.y, grab = step.grab,
                        jump = step.jump && !lastJump, jumpHeld = step.jump, dash = step.dash && !lastDash, dashHeld = step.dash };
                    lastJump = step.jump; lastDash = step.dash;
                }
        }

        // Steps until an edge exit or the first death, then holds right like the keyboard tests.
        public static int Replay(PhaseController c, IEnumerable<PhaseRouteStep> steps, int holdRight = 300)
        {
            int deaths = c.deaths;
            foreach (var input in Inputs(steps))
            {
                c.Step(input);
                if (c.deaths != deaths || c.ExitDirection != 0) return c.ExitDirection;
            }
            for (int n = 0; n < holdRight; n++)
            {
                c.Step(new Controls { x = 1 });
                if (c.deaths != deaths || c.ExitDirection != 0) return c.ExitDirection;
            }
            return 0;
        }
    }
}
