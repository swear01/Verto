using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Phase;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Records and checks keyboard routes against the PhaseController built from a scene.
// Run through Pipeline run_script; see Documentation/Development.md "Level routes".
public static class RouteTool
{
    [Serializable]
    public sealed class Goal
    {
        public bool exit, dash;
        public int minX = -1000, maxX = 1000, feet = -1, movers = -1, minY = -1000, maxY = 1000;
    }

    sealed class Node { public PhaseController c; public bool jump, dash; public Node parent; public PhaseRouteStep step; }

    public static string Validate(string scene)
    {
        var name = Path.GetFileNameWithoutExtension(scene);
        var start = Build(scene); var lines = new List<string>();
        if (File.Exists(PhaseRoute.RoutePath(name)))
        {
            var c = Clone(start); int exit = PhaseRoute.Replay(c, PhaseRoute.Parse(File.ReadAllText(PhaseRoute.RoutePath(name))));
            lines.Add($"route: {(exit == 1 && c.deaths == 0 ? "passed" : "FAILED")} exit={exit} deaths={c.deaths} frames={Math.Round(c.time * 60)} x={c.player.x} y={c.player.y}");
        }
        else lines.Add($"route: missing {PhaseRoute.RoutePath(name)}");
        if (File.Exists(PhaseRoute.BypassPath(name)))
            foreach (var bypass in PhaseRoute.ParseBypasses(File.ReadAllText(PhaseRoute.BypassPath(name))))
            {
                var c = Clone(start); int exit = PhaseRoute.Replay(c, bypass.steps, 0);
                lines.Add($"bypass {bypass.name}: {(exit == 1 ? "FAILED, reaches the exit" : "blocked")} deaths={c.deaths} x={c.player.x} y={c.player.y}");
            }
        return string.Join("\n", lines);
    }

    // Breadth-first search from the end of prefix (a route file, or "" for spawn) to goal (Goal JSON).
    // keys lists the values tried per action, e.g. "x:-1,0,1 y:-1,0 j:0,1 d:0,1 g:0"; each choice is held for frames.
    public static async Task<string> Search(string scene, string prefix, string goal, string keys, int frames, int depth, string output)
    {
        var start = Build(scene);
        var target = JsonUtility.FromJson<Goal>(goal);
        var head = string.IsNullOrEmpty(prefix) ? new PhaseRouteStep[0] : PhaseRoute.Parse(File.ReadAllText(prefix));
        return await Task.Run(() =>
        {
            var root = new Node { c = start };
            foreach (var input in PhaseRoute.Inputs(head))
            {
                root.c.Step(input);
                if (root.c.deaths > 0 || root.c.ExitDirection != 0) return "prefix dies or exits before its end";
            }
            root.jump = head.LastOrDefault()?.jump ?? false; root.dash = head.LastOrDefault()?.dash ?? false;
            var found = Bfs(root, target, Actions(keys, frames), depth);
            if (found == null) return $"no route within {depth} steps of {frames} frames";
            var path = new List<PhaseRouteStep>(); for (var n = found; n.parent != null; n = n.parent) path.Add(n.step);
            path.Reverse();
            var route = Merge(head.Concat(path));
            File.WriteAllText(output, Json(route));
            return $"found: {route.Count} steps, {route.Sum(s => s.frames)} frames, x={found.c.player.x} y={found.c.player.y} -> {output}";
        });
    }

    // Randomized local search that keeps the route winning and reduces the one-frame step shifts that break it.
    public static async Task<string> Harden(string scene, string route, int seconds, int seed, string output)
    {
        var start = Build(scene); var steps = Merge(PhaseRoute.Parse(File.ReadAllText(route)));
        return await Task.Run(() =>
        {
            int Score(List<PhaseRouteStep> q)
            {
                var c = Clone(start);
                if (PhaseRoute.Replay(c, q) != 1 || c.deaths > 0) return int.MaxValue;
                return Fragile(start, q) * 1000 + q.Count;
            }
            var rng = new System.Random(seed); int best = Score(steps), first = best;
            if (best == int.MaxValue) return "route does not clear the scene";
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                var q = Copy(steps); int i = rng.Next(q.Count);
                switch (rng.Next(5))
                {
                    case 0: if (i + 1 < q.Count) { int d = rng.Next(1, 4) * (rng.Next(2) * 2 - 1); q[i].frames += d; q[i + 1].frames -= d; } break;
                    case 1: if (i > 0) Keys(q[i], q[i - 1]); break;
                    case 2: if (i + 1 < q.Count) Keys(q[i], q[i + 1]); break;
                    case 3:
                        switch (rng.Next(4)) { case 0: q[i].x = rng.Next(3) - 1; break; case 1: q[i].y = rng.Next(3) - 1; break; case 2: q[i].jump = !q[i].jump; break; default: q[i].dash = !q[i].dash; break; }
                        break;
                    default: q[i].frames += rng.Next(2) * 2 - 1; break;
                }
                if (q.Any(s => s.frames <= 0)) continue;
                q = Merge(q); int score = Score(q);
                if (score <= best) { best = score; steps = q; }
            }
            File.WriteAllText(output, Json(steps));
            return $"fragile shifts {first / 1000} -> {best / 1000} of {2 * (steps.Count - 1)}, {steps.Count} steps -> {output}";
        });
    }

    static PhaseController Build(string scene)
    {
        var path = scene.EndsWith(".unity") ? scene : $"Assets/Scenes/{scene}.unity";
        var loaded = SceneManager.GetSceneByPath(path);
        var opened = loaded.isLoaded ? loaded : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var c = opened.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<PhaseSandbox>()).Single().CreateController();
            c.exitRight = true;
            return c;
        }
        finally { if (!loaded.isLoaded) EditorSceneManager.CloseScene(opened, true); }
    }

    static int Fragile(PhaseController start, List<PhaseRouteStep> steps)
    {
        var shifts = Enumerable.Range(0, Math.Max(0, steps.Count - 1)).SelectMany(i => new[] { (i, -1), (i, 1) });
        return shifts.AsParallel().Count(t =>
        {
            var q = Copy(steps); q[t.Item1].frames += t.Item2; q[t.Item1 + 1].frames -= t.Item2;
            if (q[t.Item1].frames <= 0 || q[t.Item1 + 1].frames <= 0) return false;
            var c = Clone(start); return PhaseRoute.Replay(c, q) != 1 || c.deaths > 0;
        });
    }

    static Node Bfs(Node root, Goal goal, PhaseRouteStep[] actions, int depth)
    {
        var seen = new HashSet<string> { Key(root) }; var frontier = new List<Node> { root }; var gate = new object();
        for (int level = 0; level < depth && frontier.Count > 0; level++)
        {
            var next = new System.Collections.Concurrent.ConcurrentBag<Node>(); Node hit = null;
            Parallel.ForEach(frontier, n =>
            {
                foreach (var a in actions)
                {
                    if (hit != null) return;
                    var child = new Node { c = Clone(n.c), jump = n.jump, dash = n.dash, parent = n, step = a };
                    if (!Advance(child, a)) continue;
                    if (Reached(child.c, goal)) { lock (gate) hit = hit ?? child; return; }
                    var p = child.c.player; if (p.y < goal.minY || p.y > goal.maxY) continue;
                    var key = Key(child); bool added; lock (gate) added = seen.Add(key);
                    if (added) next.Add(child);
                }
            });
            if (hit != null) return hit;
            frontier = next.ToList();
        }
        return null;
    }

    static bool Advance(Node n, PhaseRouteStep s)
    {
        for (int i = 0; i < s.frames; i++)
        {
            n.c.Step(new Controls { x = s.x, y = s.y, grab = s.grab, jumpHeld = s.jump, dashHeld = s.dash, jump = s.jump && !n.jump, dash = s.dash && !n.dash });
            n.jump = s.jump; n.dash = s.dash;
            if (n.c.deaths > 0 || n.c.ExitDirection == -1) return false;
            if (n.c.ExitDirection == 1) return true;
        }
        return true;
    }

    static bool Reached(PhaseController c, Goal g)
    {
        if (g.exit) return c.ExitDirection == 1;
        var p = c.player;
        return p.grounded && p.x >= g.minX && p.x <= g.maxX && (g.feet < 0 || p.y + p.h == g.feet) && (!g.dash || p.dash == 1) &&
            (g.movers < 0 || c.movers.All(m => m.index == (g.movers == 0 ? m.start : (m.start + 1) % m.nodes.Length) && !m.moving));
    }

    static string Key(Node n)
    {
        var p = n.c.player; var m = n.c.movers.FirstOrDefault();
        return string.Join(",", Math.Round(p.x / 2), Math.Round(p.y / 2), Math.Round(p.vx / 20), Math.Round(p.vy / 20), p.dash, p.climbing, (int)(p.stamina / 20),
            n.jump, n.dash, p.dashTime > 0, p.varTime > 1e-9, p.wallLock > 0, p.ducking, p.autoJump, n.c.freeze > 1e-9, p.dashCooldown > 0, p.coyote > 0,
            m == null ? "" : $"{m.index}/{(m.moving ? (int)(m.elapsed * 60) : -1)}/{m.pending}",
            string.Join("", n.c.gears.Select(g => g.wall.y)), string.Join("", n.c.crystals.Select(c => c.collected ? 1 : 0)), p.airJump);
    }

    static PhaseRouteStep[] Actions(string keys, int frames)
    {
        var values = keys.Split(' ').ToDictionary(t => t[0], t => t.Substring(2).Split(',').Select(int.Parse).ToArray());
        return (from x in values['x'] from y in values['y'] from j in values['j'] from d in values['d'] from g in values['g']
                select new PhaseRouteStep { frames = frames, x = x, y = y, jump = j != 0, dash = d != 0, grab = g != 0 }).ToArray();
    }

    static void Keys(PhaseRouteStep to, PhaseRouteStep from) { to.x = from.x; to.y = from.y; to.jump = from.jump; to.dash = from.dash; to.grab = from.grab; }
    static List<PhaseRouteStep> Copy(IEnumerable<PhaseRouteStep> steps) =>
        steps.Select(s => new PhaseRouteStep { frames = s.frames, x = s.x, y = s.y, jump = s.jump, dash = s.dash, grab = s.grab }).ToList();

    static List<PhaseRouteStep> Merge(IEnumerable<PhaseRouteStep> steps)
    {
        var merged = new List<PhaseRouteStep>();
        foreach (var s in Copy(steps))
        {
            var last = merged.LastOrDefault();
            if (last != null && last.x == s.x && last.y == s.y && last.jump == s.jump && last.dash == s.dash && last.grab == s.grab) last.frames += s.frames;
            else merged.Add(s);
        }
        return merged;
    }

    static string Json(IEnumerable<PhaseRouteStep> steps) => "[\n" + string.Join(",\n", steps.Select(s =>
        $"  {{\"frames\":{s.frames},\"x\":{s.x},\"y\":{s.y},\"jump\":{(s.jump ? "true" : "false")},\"dash\":{(s.dash ? "true" : "false")},\"grab\":{(s.grab ? "true" : "false")}}}")) + "\n]\n";

    // Search nodes branch by deep-copying the pure C# controller state.
    static PhaseController Clone(PhaseController c) => (PhaseController)Copy(c, new Dictionary<object, object>(new Same()));
    sealed class Same : IEqualityComparer<object>
    {
        public new bool Equals(object a, object b) => ReferenceEquals(a, b);
        public int GetHashCode(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
    }
    static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, FieldInfo[]> fields = new System.Collections.Concurrent.ConcurrentDictionary<Type, FieldInfo[]>();
    static FieldInfo[] Fields(Type type) => fields.GetOrAdd(type, t =>
    {
        var list = new List<FieldInfo>();
        for (var x = t; x != null; x = x.BaseType) list.AddRange(x.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
        return list.ToArray();
    });
    static object Copy(object o, Dictionary<object, object> memo)
    {
        if (o == null) return null;
        var t = o.GetType();
        if (t.IsPrimitive || t.IsEnum || o is string || t.Name.Contains("Comparer")) return o;
        if (memo.TryGetValue(o, out var done)) return done;
        if (t.IsArray)
        {
            var a = (Array)o; var b = Array.CreateInstance(t.GetElementType(), a.Length); memo[o] = b;
            for (int i = 0; i < a.Length; i++) b.SetValue(Copy(a.GetValue(i), memo), i);
            return b;
        }
        var c = t.IsValueType ? Activator.CreateInstance(t) : System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
        if (!t.IsValueType) memo[o] = c;
        foreach (var f in Fields(t)) f.SetValue(c, Copy(f.GetValue(o), memo));
        return c;
    }
}
