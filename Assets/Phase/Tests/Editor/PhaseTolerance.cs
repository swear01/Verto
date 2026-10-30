using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Phase;

// Finds a winning keyboard route through a level with the real controller, then measures how many frames each input on it
// may move and still win. See Documentation/Tolerance.md.
public static class PhaseTolerance
{
    // Window grades in frames at 60 Hz: tight is near frame-perfect, free needs no precision.
    public const int TightMax = 3, FreeMin = 13, Reach = 20;
    public enum Kind { Press, Release, Turn }
    public struct Input { public int x; public bool held; }

    public sealed class Step
    {
        public Kind kind;
        public int frame, value, lo, hi, phase;
        public bool loCapped, hiCapped;
        public string contact;
        public double x, xLo, xHi;
        public int Window => hi - lo + 1;
        public string Grade => Window <= TightMax ? "tight" : Window >= FreeMin ? "free" : "moderate";
    }

    public sealed class Result
    {
        public string level, error;
        public List<Input> route;
        public List<Step> steps = new List<Step>();
        public int expansions, jumps;
        public double seconds;
        public Step Tightest => steps.OrderBy(s => s.Window).ThenBy(s => s.frame).FirstOrDefault();
        public int Count(string grade) => steps.Count(s => s.Grade == grade);
        public string Verdict => error != null ? "unsolved" : Count("tight") > 0 ? "too tight" : Count("moderate") == 0 ? "too loose" : "in band";
    }

    // ---- Controller snapshots: every mutable value field, so new controller state is copied without changes here.

    sealed class State
    {
        public object[] values;
        public Player player;
        public int[] blocked;
        public bool[] crystals;
        public object[][] platforms;
        public double[] attached;
        public bool held;
    }
    static FieldInfo[] Fields(Type t) => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(f => f.FieldType.IsValueType && !f.IsInitOnly).ToArray();
    static readonly FieldInfo[] ControllerFields = Fields(typeof(PhaseController)), PlatformFields = Fields(typeof(Platform));
    static readonly MethodInfo Clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

    static State Save(PhaseController c, bool held) => new State
    {
        values = ControllerFields.Select(f => f.GetValue(c)).ToArray(),
        player = (Player)Clone.Invoke(c.player, null),
        blocked = c.blocked.ToArray(),
        crystals = c.crystals.Select(k => k.collected).ToArray(),
        platforms = c.platforms.Select(p => PlatformFields.Select(f => f.GetValue(p)).ToArray()).ToArray(),
        attached = c.platforms.SelectMany(p => p.attached).SelectMany(s => new[] { s.x, s.y }).ToArray(),
        held = held,
    };

    static void Load(PhaseController c, State s)
    {
        for (int i = 0; i < ControllerFields.Length; i++) ControllerFields[i].SetValue(c, s.values[i]);
        c.player = (Player)Clone.Invoke(s.player, null);
        c.blocked.Clear(); c.blocked.UnionWith(s.blocked);
        for (int i = 0; i < c.crystals.Count; i++) c.crystals[i].collected = s.crystals[i];
        for (int n = 0; n < c.platforms.Count; n++)
            for (int i = 0; i < PlatformFields.Length; i++) PlatformFields[i].SetValue(c.platforms[n], s.platforms[n][i]);
        int a = 0;
        foreach (var spike in c.platforms.SelectMany(p => p.attached)) { spike.x = s.attached[a++]; spike.y = s.attached[a++]; }
        c.particles.Clear();
    }

    static bool Won(PhaseController c) => c.won || c.ExitDirection > 0;
    // One frame with keyboard semantics: jump fires on the first held frame.
    static void Tick(PhaseController c, Input input, bool previous)
    {
        c.Step(new Controls { x = input.x, jump = input.held && !previous, jumpHeld = input.held });
        c.particles.Clear();
    }

    // ---- Route search: weighted A* over held inputs that change every `macro` frames, with states merged on a coarse key.

    static readonly Input[] Actions = { new Input { x = 1, held = true }, new Input { x = 1 }, new Input { x = 0, held = true },
        new Input { x = 0 }, new Input { x = -1, held = true }, new Input { x = -1 } };

    static long Key(PhaseController c, bool held)
    {
        var p = c.player;
        long h = 17;
        void Mix(long v) { unchecked { h = h * 1000003 + v; } }
        Mix((long)Math.Round(p.x)); Mix((long)Math.Round(p.y)); Mix((long)Math.Round(p.vx / 10)); Mix((long)Math.Round(p.vy / 10));
        Mix(c.phase); Mix(p.grounded ? 1 : 0); Mix(held ? 1 : 0); Mix(p.wall); Mix(p.varTime > 0 ? 1 : 0); Mix(p.wallLock > 0 ? 1 : 0);
        Mix(p.airJump); Mix(p.coyote > 0 ? 1 : 0); Mix((long)(p.slide * 10));
        foreach (var platform in c.platforms) { Mix((long)platform.x); Mix((long)platform.y); Mix(platform.queued); }
        foreach (var crystal in c.crystals) Mix(crystal.collected ? 1 : 0);
        foreach (int b in c.blocked) Mix(b + 7);
        return h;
    }

    public static List<Input> Solve(PhaseController c, out int expansions, int macro = 4, int budget = 150000, double greed = 3, double jumpCost = 6)
    {
        var start = Save(c, false); int deaths = c.deaths;
        var nodes = new List<(State state, int parent, Input input, int frames)> { (start, -1, default, 0) };
        var open = new SortedSet<(double f, int id)> { (0, 0) };
        var seen = new HashSet<long> { Key(c, false) };
        expansions = 0;
        try
        {
            while (open.Count > 0 && expansions < budget)
            {
                var top = open.Min; open.Remove(top); expansions++;
                var node = nodes[top.id];
                foreach (var input in Actions)
                {
                    Load(c, node.state);
                    bool previous = node.state.held; int ran = 0; bool dead = false, won = false;
                    for (; ran < macro && !dead && !won; ran++)
                    {
                        Tick(c, input, previous); previous = input.held;
                        dead = c.deaths != deaths || c.respawn > 0; won = !dead && Won(c);
                    }
                    if (dead) continue;
                    if (won)
                    {
                        var route = Enumerable.Repeat(input, ran).ToList();
                        for (int id = top.id; nodes[id].parent >= 0; id = nodes[id].parent) route.InsertRange(0, Enumerable.Repeat(nodes[id].input, macro));
                        return route;
                    }
                    if (!seen.Add(Key(c, input.held))) continue;
                    int frames = node.frames + macro;
                    nodes.Add((Save(c, input.held), top.id, input, frames));
                    open.Add((frames + jumpCost * c.jumps + greed * (c.Width - c.player.x) / 1.5, nodes.Count - 1));
                }
            }
            return null;
        }
        finally { Load(c, start); }
    }

    // ---- Replays.

    // Plays a frame route from the controller's current state; true when it wins without dying.
    static bool Wins(PhaseController c, List<Input> route, int tail = 300)
    {
        var start = Save(c, false); int deaths = c.deaths;
        try
        {
            bool previous = false;
            for (int i = 0; i < route.Count + tail; i++)
            {
                var input = i < route.Count ? route[i] : new Input { x = 1 };
                Tick(c, input, previous); previous = input.held;
                if (c.deaths != deaths || c.respawn > 0) return false;
                if (Won(c)) return true;
            }
            return false;
        }
        finally { Load(c, start); }
    }

    // Holds each jump at least through the variable-height window and drops needless left/still runs, as a player would.
    static List<Input> Simplify(PhaseController c, List<Input> route)
    {
        route = new List<Input>(route);
        for (int i = route.Count - 1; i >= 0; i--)
        {
            if (!route[i].held || (i > 0 && route[i - 1].held)) continue;
            var tried = new List<Input>(route);
            for (int j = i; j < tried.Count && tried[j].held; j++) tried[j] = new Input { x = tried[j].x };
            if (Wins(c, tried)) route = tried;
        }
        for (int i = 0; i < route.Count; i++)
        {
            if (!route[i].held || (i > 0 && route[i - 1].held)) continue;
            var tried = new List<Input>(route);
            for (int j = i; j < Math.Min(i + 12, tried.Count); j++)
            {
                if (j > i && !route[j - 1].held && route[j].held) break;
                tried[j] = new Input { x = tried[j].x, held = true };
            }
            if (Wins(c, tried)) route = tried;
        }
        for (int i = 0; i < route.Count;)
        {
            int end = i; while (end < route.Count && route[end].x == route[i].x) end++;
            if (route[i].x != 1)
            {
                var tried = new List<Input>(route);
                for (int j = i; j < end; j++) tried[j] = new Input { x = 1, held = tried[j].held };
                if (Wins(c, tried)) route = tried;
            }
            i = end;
        }
        return route;
    }

    sealed class Event { public Kind kind; public int frame, value, pair = -1; public bool byTime; }

    sealed class Reference
    {
        public List<Input> route;
        public List<Event> events = new List<Event>();
        public State[] states;
        public double[] x, y, vx;
        public bool[] grounded;
        public int[] wall, phase;
    }

    static string Contact(bool grounded, int wall) => grounded ? "ground" : wall != 0 ? "wall" : "air";

    static Reference Trace(PhaseController c, List<Input> route)
    {
        var r = new Reference { route = route, states = new State[route.Count + 1], x = new double[route.Count + 1], y = new double[route.Count + 1],
            vx = new double[route.Count + 1], grounded = new bool[route.Count + 1], wall = new int[route.Count + 1], phase = new int[route.Count + 1] };
        var start = Save(c, false);
        bool previous = false;
        for (int i = 0; i <= route.Count; i++)
        {
            var p = c.player;
            r.states[i] = Save(c, previous); r.x[i] = p.x; r.y[i] = p.y; r.vx[i] = p.vx; r.grounded[i] = p.grounded || p.coyote > 0; r.wall[i] = p.wall; r.phase[i] = c.phase;
            if (i == route.Count) break;
            Tick(c, route[i], previous); previous = route[i].held;
        }
        Load(c, start);
        int press = -1;
        for (int i = 1; i < route.Count; i++)
        {
            if (route[i].x != route[i - 1].x) r.events.Add(new Event { kind = Kind.Turn, frame = i, value = route[i].x });
            if (route[i].held != route[i - 1].held)
            {
                r.events.Add(new Event { kind = route[i].held ? Kind.Press : Kind.Release, frame = i, value = route[i].held ? 1 : 0, pair = route[i].held ? -1 : press });
                if (route[i].held) press = r.events.Count - 1;
            }
        }
        // A position anchor is kept only where it first holds at the event's own frame, so the unshifted replay is the reference.
        for (int k = 0; k < r.events.Count; k++)
        {
            var e = r.events[k];
            if (e.kind == Kind.Release || Math.Abs(r.vx[e.frame]) < 30) { e.byTime = true; continue; }
            int from = k > 0 ? r.events[k - 1].frame : 0;
            for (int f = from; f < e.frame && !e.byTime; f++) e.byTime = Anchored(r, e, r.x[f], r.y[f], r.grounded[f], r.wall[f]);
        }
        return r;
    }

    // Whether a position-anchored event is due for a player at (x, y) with this contact.
    static bool Anchored(Reference r, Event e, double x, double y, bool grounded, int wall)
    {
        int f = e.frame;
        if ((x - r.x[f]) * Math.Sign(r.vx[f]) < 0 || Math.Abs(y - r.y[f]) > 16) return false;
        if (e.kind != Kind.Press) return true;
        return r.grounded[f] ? grounded : r.wall[f] != 0 ? wall == r.wall[f] : true;
    }

    // Replays the reference with event `p` moved by `shift` frames. Later presses and turns follow the player: each fires where
    // the player reaches the reference spot; a release keeps its hold time. Returns the frame route played, or null on failure.
    static List<Input> Play(PhaseController c, Reference r, int p, int shift, out double firedX)
    {
        firedX = double.NaN;
        var events = r.events; var target = events[p];
        int begin = Math.Min(target.frame, target.frame + shift), deaths = c.deaths;
        var fired = new int[events.Count];
        int next = 0; while (next < events.Count && events[next].frame < begin) { fired[next] = events[next].frame; next++; }
        var played = r.route.Take(begin).ToList();
        var input = begin > 0 ? r.route[begin - 1] : r.route[0];
        Load(c, r.states[begin]);
        bool previous = r.states[begin].held;
        int limit = r.route.Count + 600;
        bool done = false;
        try
        {
            for (int f = begin; f < limit; f++)
            {
                var pl = c.player;
                // The moved input fires on its own clock; earlier inputs keep theirs, later ones wait for it.
                if (!done && f >= target.frame + shift) { firedX = pl.x; Apply(target, ref input); fired[p] = f; done = true; }
                while (next < events.Count)
                {
                    if (next == p) { if (!done) break; next++; continue; }
                    var e = events[next]; bool due;
                    if (next < p) due = f >= e.frame;
                    else if (!done) break;
                    else if (e.kind == Kind.Release && e.pair >= 0) due = f >= fired[e.pair] + (e.frame - events[e.pair].frame);
                    else if (e.byTime) due = f >= fired[next - 1] + (e.frame - events[next - 1].frame);
                    else due = Anchored(r, e, pl.x, pl.y, pl.grounded || pl.coyote > 0, pl.wall);
                    if (!due)
                    {
                        if (next > p && f > fired[next - 1] + (e.frame - events[next - 1].frame) + 90) return null;
                        break;
                    }
                    Apply(e, ref input);
                    fired[next++] = f;
                }
                if (next >= events.Count && f >= r.route.Count + 30) input = new Input { x = 1 };
                played.Add(input);
                Tick(c, input, previous); previous = input.held;
                if (c.deaths != deaths || c.respawn > 0) return null;
                if (Won(c)) return played;
            }
            return null;
        }
        finally { Load(c, r.states[0]); }
    }

    static void Apply(Event e, ref Input input) { if (e.kind == Kind.Turn) input.x = e.value; else input.held = e.value == 1; }

    // ---- Analysis.

    public static Result Analyze(string level, PhaseController c, int macro = 4, int budget = 150000)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = new Result { level = level };
        var route = Solve(c, out result.expansions, macro, budget);
        if (route == null && macro > 2) route = Solve(c, out result.expansions, 2, budget);
        if (route == null) { result.error = $"no route within {result.expansions} expanded states"; result.seconds = watch.Elapsed.TotalSeconds; return result; }
        route = Simplify(c, route);
        var r = Trace(c, route);
        for (int p = 0; p < r.events.Count; p++)
        {
            var e = r.events[p];
            if (Play(c, r, p, 0, out _) == null) { result.error = $"replay of event {p} at frame {e.frame} diverged"; break; }
            // An input may not move past the previous input on the same key.
            int min = 1 - e.frame;
            for (int k = p - 1; k >= 0; k--)
                if ((r.events[k].kind == Kind.Turn) == (e.kind == Kind.Turn)) { min = r.events[k].frame + 1 - e.frame; break; }
            int lo = 0, hi = 0; double xLo = r.x[e.frame], xHi = xLo;
            while (hi < Reach && Play(c, r, p, hi + 1, out double fx) != null) { hi++; xHi = fx; }
            while (lo > Math.Max(-Reach, min) && Play(c, r, p, lo - 1, out double fx) != null) { lo--; xLo = fx; }
            bool loCapped = lo <= Math.Max(-Reach, min), hiCapped = hi >= Reach;
            // Recentre the input in its window, as a careful player would, before measuring the inputs after it.
            int centre = (lo + hi) / 2;
            if (centre != 0 && Play(c, r, p, centre, out _) is List<Input> relaxed)
            {
                int count = r.events.Count;
                var moved = Trace(c, relaxed);
                if (moved.events.Count == count) { r = moved; lo -= centre; hi -= centre; e = r.events[p]; }
            }
            result.steps.Add(new Step { kind = e.kind, frame = e.frame, value = e.value, lo = lo, hi = hi, loCapped = loCapped, hiCapped = hiCapped,
                contact = Contact(r.grounded[e.frame], r.wall[e.frame]), x = r.x[e.frame], xLo = Math.Min(xLo, xHi), xHi = Math.Max(xLo, xHi),
                phase = r.phase[Math.Min(e.frame + 1, r.route.Count)] });
        }
        result.route = r.route;
        result.jumps = r.events.Count(e => e.kind == Kind.Press);
        if (result.error == null && !Wins(c, r.route)) result.error = "final route does not win";
        result.seconds = watch.Elapsed.TotalSeconds;
        return result;
    }

    // ---- Report.

    static readonly string[] Colors = { "red", "blue", "green" };
    static string Describe(Step s) => s.kind == Kind.Press ? "jump" : s.kind == Kind.Release ? "release" : s.value > 0 ? "right" : s.value < 0 ? "left" : "stop";

    public static string Markdown(IEnumerable<Result> results)
    {
        var text = new StringBuilder();
        text.AppendLine("# Input tolerance report").AppendLine()
            .AppendLine($"Window = frames (60 Hz) an input may move and still win; tight ≤ {TightMax}, moderate {TightMax + 1}–{FreeMin - 1}, free ≥ {FreeMin}. " +
                $"Search reach is ±{Reach} frames. See Documentation/Tolerance.md.").AppendLine()
            .AppendLine("| Level | Verdict | Jumps | Steps | Free | Moderate | Tight | Tightest step |").AppendLine("|---|---|---|---|---|---|---|---|");
        var list = results.ToList();
        foreach (var r in list)
        {
            var t = r.Tightest;
            text.AppendLine(r.error != null && r.steps.Count == 0 ? $"| {r.level} | unsolved | – | – | – | – | – | {r.error} |"
                : $"| {r.level} | {r.Verdict} | {r.jumps} | {r.steps.Count} | {r.Count("free")} | {r.Count("moderate")} | {r.Count("tight")} | " +
                  (t == null ? "–" : $"{Describe(t)} at x={t.x:F0}: {t.Window}f |"));
        }
        foreach (var r in list)
        {
            text.AppendLine().AppendLine($"## {r.level}").AppendLine();
            if (r.error != null) text.AppendLine($"Problem: {r.error}.").AppendLine();
            if (r.route == null) { text.AppendLine($"Search time {r.seconds:F1}s, {r.expansions} states."); continue; }
            text.AppendLine($"Route {r.route.Count} frames ({r.route.Count / 60.0:F1}s), {r.jumps} jumps; {r.expansions} states searched, {r.seconds:F1}s.").AppendLine();
            var bins = new[] { (1, 3), (4, 6), (7, 9), (10, 12), (13, 2 * Reach + 1) };
            text.AppendLine("Histogram: " + string.Join(", ", bins.Select(b => $"{(b.Item2 > 40 ? $"{b.Item1}+" : $"{b.Item1}–{b.Item2}")}f {r.steps.Count(s => s.Window >= b.Item1 && s.Window <= b.Item2)}"))).AppendLine();
            text.AppendLine("| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |").AppendLine("|---|---|---|---|---|---|---|---|---|");
            for (int i = 0; i < r.steps.Count; i++)
            {
                var s = r.steps[i];
                text.AppendLine($"| {i + 1} | {s.frame} | {Describe(s)} | {s.x:F0} | {s.contact} | {(s.kind == Kind.Press && s.phase >= 0 ? Colors[s.phase] : "")} | " +
                    $"{s.Window} ({(s.loCapped ? "≤" : "")}{s.lo}..{(s.hiCapped ? "≥" : "")}+{s.hi}) | {s.xHi - s.xLo:F0} | {s.Grade} |");
            }
        }
        return text.ToString();
    }

#if UNITY_EDITOR
    // Builds each level scene's controller as Play does and writes the report. Classmate levels are skipped.
    public static readonly string[] Classmate = { "PhaseDemo1.1", "PhaseDemo12", "PhaseDemo13", "PhaseDemo14" };
    public static string WriteReport(string path = "Verification/ToleranceReport.md", params string[] levels)
    {
        if (levels.Length == 0) levels = PhaseSandbox.Levels.Except(Classmate).ToArray();
        var results = new List<Result>();
        foreach (var level in levels)
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"Assets/Scenes/{level}.unity");
            var sandbox = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>();
            var result = Analyze(level, sandbox.CreateController());
            UnityEngine.Debug.Log($"{level}: {result.Verdict}, {result.steps.Count} steps, tight {result.Count("tight")}, {result.seconds:F1}s");
            results.Add(result);
        }
        System.IO.File.WriteAllText(path, Markdown(results));
        return string.Join("\n", results.Select(r => $"{r.level}: {r.Verdict}"));
    }
#endif
}
