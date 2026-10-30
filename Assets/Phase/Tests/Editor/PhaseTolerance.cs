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
    // A window stops growing at Enough frames or Reach frames to one side; either is reported as a lower bound.
    public const int TightMax = 3, FreeMin = 13, Enough = 17, Reach = 20;
    // A moved input counts as recovered once this many later jumps fire and the player lives half a second more.
    public const int Horizon = 2;
    // Route searches tried per level as (decision frames, cost per jump, cost per direction change); the most forgiving route is kept.
    static readonly (int macro, double jumpCost, double turnCost)[] Searches = { (4, 6, 8), (4, 6, 0), (6, 20, 8) };
    public enum Kind { Press, Release, Turn }
    public struct Input { public int x; public bool held; }

    public sealed class Step
    {
        public Kind kind;
        public int frame, value, lo, hi, phase;
        public bool loCapped, hiCapped, jumps;
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
        public int expansions, jumps, routes;
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

    public static List<Input> Solve(PhaseController c, out int expansions, int macro = 4, int budget = 60000, double greed = 3, double jumpCost = 6, double turnCost = 8)
    {
        var start = Save(c, false); int deaths = c.deaths;
        var nodes = new List<(State state, int parent, Input input, int frames, int turns)> { (start, -1, new Input { x = 1 }, 0, 0) };
        var open = new SortedSet<(double f, int id)> { (0, 0) };
        var seen = new HashSet<long> { Key(c, false) };
        // Frames to the nearest standing spot on the right edge, as the crow flies at running speed.
        var exits = c.blocks.Where(b => b.x + b.w >= c.Width && b.y >= 11).Select(b => b.y - 11).Distinct().ToList();
        double Distance(Player p) => (c.Width - p.x + (exits.Count == 0 ? 0 : exits.Min(y => Math.Abs(y - p.y)))) / 1.5;
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
                    int frames = node.frames + macro, turns = node.turns + (input.x != node.input.x ? 1 : 0);
                    nodes.Add((Save(c, input.held), top.id, input, frames, turns));
                    open.Add((frames + jumpCost * c.jumps + turnCost * turns + greed * Distance(c.player), nodes.Count - 1));
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
    // Whether the press at `frame` starts a jump, at once or from the jump buffer.
    static bool Jumps(Reference r, int frame) => Enumerable.Range(frame + 1, 6).Any(f => f < r.phase.Length && r.phase[f] != r.phase[frame]);

    sealed class Reference
    {
        public List<Input> route;
        public List<Event> events = new List<Event>();
        public State[] states;
        public double[] x, y, vx, vy;
        public bool[] grounded;
        public int[] wall, phase;
    }

    static string Contact(bool grounded, int wall) => grounded ? "ground" : wall != 0 ? "wall" : "air";

    // Records the state before every frame; with `prior`, frames before `from` are copied from it because the routes share them.
    static Reference Trace(PhaseController c, List<Input> route, Reference prior = null, int from = 0)
    {
        var r = new Reference { route = route, states = new State[route.Count + 1], x = new double[route.Count + 1], y = new double[route.Count + 1],
            vx = new double[route.Count + 1], vy = new double[route.Count + 1], grounded = new bool[route.Count + 1], wall = new int[route.Count + 1], phase = new int[route.Count + 1] };
        var start = Save(c, false);
        if (prior == null) from = 0;
        else
        {
            Array.Copy(prior.states, r.states, from); Array.Copy(prior.x, r.x, from); Array.Copy(prior.y, r.y, from); Array.Copy(prior.vx, r.vx, from); Array.Copy(prior.vy, r.vy, from);
            Array.Copy(prior.grounded, r.grounded, from); Array.Copy(prior.wall, r.wall, from); Array.Copy(prior.phase, r.phase, from);
            Load(c, prior.states[from]);
        }
        bool previous = from > 0 && route[from - 1].held;
        for (int i = from; i <= route.Count; i++)
        {
            var p = c.player;
            r.states[i] = Save(c, previous); r.x[i] = p.x; r.y[i] = p.y; r.vx[i] = p.vx; r.vy[i] = p.vy; r.grounded[i] = p.grounded || p.coyote > 0; r.wall[i] = p.wall; r.phase[i] = c.phase;
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
            if (e.kind == Kind.Release || (Math.Abs(r.vx[e.frame]) < 30 && Math.Abs(r.vy[e.frame]) < 30)) { e.byTime = true; continue; }
            int first = k > 0 ? r.events[k - 1].frame : 0;
            for (int f = first; f < e.frame && !e.byTime; f++) e.byTime = Anchored(r, e, r.x[f], r.y[f], r.grounded[f], r.wall[f]);
        }
        return r;
    }

    // Whether a position-anchored event is due for a player at (x, y) with this contact: the player has passed the reference
    // spot along its main direction of travel there and is near it across that direction.
    static bool Anchored(Reference r, Event e, double x, double y, bool grounded, int wall)
    {
        int f = e.frame;
        bool across = Math.Abs(r.vx[f]) >= 30;
        double along = across ? (x - r.x[f]) * Math.Sign(r.vx[f]) : (y - r.y[f]) * Math.Sign(r.vy[f]);
        if (along < 0 || Math.Abs(across ? y - r.y[f] : x - r.x[f]) > 16) return false;
        if (e.kind != Kind.Press) return true;
        return r.grounded[f] ? grounded : r.wall[f] != 0 ? wall == r.wall[f] : true;
    }

    // Replays the reference with event `p` moved by `shift` frames. Later presses and turns follow the player: each fires where
    // the player reaches the reference spot; a release keeps its hold time. Returns the frame route played, or null on failure.
    static List<Input> Play(PhaseController c, Reference r, int p, int shift, out double firedX, int horizon = Horizon, int start = int.MaxValue)
    {
        firedX = double.NaN;
        var events = r.events; var target = events[p];
        int begin = Math.Min(Math.Min(target.frame, target.frame + shift), start), deaths = c.deaths;
        var fired = new int[events.Count];
        int next = 0; while (next < events.Count && events[next].frame < begin) { fired[next] = events[next].frame; next++; }
        var played = r.route.Take(begin).ToList();
        var input = begin > 0 ? r.route[begin - 1] : r.route[0];
        Load(c, r.states[begin]);
        bool previous = r.states[begin].held;
        int limit = r.route.Count + 600;
        bool done = false;
        int jumps = 0, settled = int.MaxValue;
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
                    if (e.kind == Kind.Press && ++jumps == horizon) settled = f + 30;
                    fired[next++] = f;
                }
                if (next >= events.Count && f >= r.route.Count + 30) input = new Input { x = 1 };
                played.Add(input);
                Tick(c, input, previous); previous = input.held;
                if (c.deaths != deaths || c.respawn > 0) return null;
                if (Won(c) || f >= settled) return played;
            }
            return null;
        }
        finally { Load(c, r.states[0]); }
    }

    // The reference without the events at `a` and `b`; later inputs keep their position anchors.
    static Reference Without(Reference r, int a, int b)
    {
        var copy = new Reference { route = r.route, states = r.states, x = r.x, y = r.y, vx = r.vx, vy = r.vy, grounded = r.grounded, wall = r.wall, phase = r.phase };
        copy.events = r.events.Where((e, i) => i != a && i != b).Select(e => new Event { kind = e.kind, frame = e.frame, value = e.value, byTime = e.byTime,
            pair = e.pair < 0 ? -1 : e.pair - (e.pair > a ? 1 : 0) - (e.pair > b ? 1 : 0) }).ToList();
        return copy;
    }

    // Drops each detour (a stop or step back and the return to running right, or a release and the next press) when the route
    // still wins with later inputs fired at the same places, so the measured route keeps only the inputs it needs.
    static Reference DropDetours(PhaseController c, Reference r)
    {
        for (int a = 0; a < r.events.Count; a++)
        {
            var kind = r.events[a].kind;
            if (kind == Kind.Press || (kind == Kind.Turn && r.events[a].value == 1)) continue;
            int b = a + 1; while (b < r.events.Count && (r.events[b].kind == Kind.Turn) != (kind == Kind.Turn)) b++;
            if (b >= r.events.Count || (kind == Kind.Turn && r.events[b].value != 1)) continue;
            var without = Without(r, a, b);
            int from = r.events[a].frame, p = without.events.FindIndex(e => e.frame >= from);
            var route = p < 0 ? null : Play(c, without, p, 0, out _, int.MaxValue, from);
            if (route == null) continue;
            var next = Trace(c, route, r, from);
            if (next.events.Count != r.events.Count - 2) continue;
            r = next; a--;
        }
        return r;
    }

    static void Apply(Event e, ref Input input) { if (e.kind == Kind.Turn) input.x = e.value; else input.held = e.value == 1; }

    // ---- Analysis.

    // Analyzes the route of each search setting and keeps the one with the fewest tight steps, then the widest tightest window.
    public static Result Analyze(string level, PhaseController c, int budget = 60000)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Result best = null; int expansions = 0;
        var tried = new HashSet<string>();
        foreach (var (macro, jumpCost, turnCost) in Searches)
        {
            var route = Solve(c, out int searched, macro, budget, jumpCost: jumpCost, turnCost: turnCost);
            expansions += searched;
            if (route == null) continue;
            var r = DropDetours(c, Trace(c, Simplify(c, route)));
            if (!tried.Add(string.Join(",", r.events.Select(e => $"{e.kind}{e.frame}")))) continue;
            var result = Measure(level, c, r);
            if (best == null || (result.error == null && (best.error != null || Score(result).CompareTo(Score(best)) > 0))) best = result;
        }
        best = best ?? new Result { level = level, error = $"no route within {budget} expanded states per search" };
        best.expansions = expansions; best.routes = tried.Count; best.seconds = watch.Elapsed.TotalSeconds;
        return best;
    }
    static (int, int, int) Score(Result r) => (-r.Count("tight"), r.Tightest?.Window ?? int.MaxValue, -r.Count("moderate") - r.Count("tight"));

    static Result Measure(string level, PhaseController c, Reference r)
    {
        var result = new Result { level = level };
        for (int p = 0; p < r.events.Count; p++)
        {
            var e = r.events[p];
            if (Play(c, r, p, 0, out _) == null) { result.error = $"replay of event {p} at frame {e.frame} diverged"; break; }
            // An input may not move past the previous input on the same key.
            int min = 1 - e.frame;
            for (int k = p - 1; k >= 0; k--)
                if ((r.events[k].kind == Kind.Turn) == (e.kind == Kind.Turn)) { min = r.events[k].frame + 1 - e.frame; break; }
            int lo = 0, hi = 0; double xLo = r.x[e.frame], xHi = xLo;
            bool late = true, early = lo > Math.Max(-Reach, min);
            while ((late || early) && hi - lo + 1 < Enough)
            {
                if (late) { if (hi < Reach && Play(c, r, p, hi + 1, out double fx) != null) { hi++; xHi = fx; } else late = false; }
                if (early && hi - lo + 1 < Enough) { if (lo > Math.Max(-Reach, min) && Play(c, r, p, lo - 1, out double fx) != null) { lo--; xLo = fx; } else early = false; }
            }
            bool loCapped = early || lo <= Math.Max(-Reach, min), hiCapped = late || hi >= Reach;
            // Recentre the input in its window, as a careful player would, before measuring the inputs after it.
            int centre = (lo + hi) / 2;
            if (centre != 0 && Play(c, r, p, centre, out _, int.MaxValue) is List<Input> relaxed)
            {
                int count = r.events.Count;
                var moved = Trace(c, relaxed, r, Math.Min(e.frame, e.frame + centre));
                if (moved.events.Count == count) { r = moved; lo -= centre; hi -= centre; e = r.events[p]; }
            }
            result.steps.Add(new Step { kind = e.kind, frame = e.frame, value = e.value, lo = lo, hi = hi, loCapped = loCapped, hiCapped = hiCapped,
                contact = Contact(r.grounded[e.frame], r.wall[e.frame]), x = r.x[e.frame], xLo = Math.Min(xLo, xHi), xHi = Math.Max(xLo, xHi),
                jumps = e.kind == Kind.Press && Jumps(r, e.frame), phase = r.phase[Math.Min(e.frame + 6, r.route.Count)] });
        }
        result.route = r.route;
        result.jumps = r.events.Count(e => e.kind == Kind.Press && Jumps(r, e.frame));
        if (result.error == null && !Wins(c, r.route)) result.error = "final route does not win";
        return result;
    }

    // ---- Report.

    static readonly string[] Colors = { "red", "blue", "green" };
    static string Describe(Step s) => s.kind == Kind.Press ? (s.jumps ? "jump" : "press") : s.kind == Kind.Release ? "release" : s.value > 0 ? "right" : s.value < 0 ? "left" : "stop";

    public static string Markdown(IEnumerable<Result> results)
    {
        var text = new StringBuilder();
        text.AppendLine("# Input tolerance report").AppendLine()
            .AppendLine($"Window = frames (60 Hz) an input may move and still win; tight ≤ {TightMax}, moderate {TightMax + 1}–{FreeMin - 1}, free ≥ {FreeMin}. " +
                $"A window stops growing at {Enough} frames or ±{Reach}; ≤/≥ mark such bounds. See Documentation/Tolerance.md.").AppendLine()
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
            text.AppendLine($"Route {r.route.Count} frames ({r.route.Count / 60.0:F1}s), {r.jumps} jumps; most forgiving of {r.routes} routes from {r.expansions} searched states, {r.seconds:F0}s.").AppendLine();
            var bins = new[] { (1, 3), (4, 6), (7, 9), (10, 12), (13, 2 * Reach + 1) };
            text.AppendLine("Histogram: " + string.Join(", ", bins.Select(b => $"{(b.Item2 > 40 ? $"{b.Item1}+" : $"{b.Item1}–{b.Item2}")}f {r.steps.Count(s => s.Window >= b.Item1 && s.Window <= b.Item2)}"))).AppendLine();
            text.AppendLine("| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |").AppendLine("|---|---|---|---|---|---|---|---|---|");
            for (int i = 0; i < r.steps.Count; i++)
            {
                var s = r.steps[i];
                text.AppendLine($"| {i + 1} | {s.frame} | {Describe(s)} | {s.x:F0} | {s.contact} | {(s.jumps ? Colors[s.phase] : "")} | " +
                    $"{s.Window} ({(s.loCapped ? "≤" : "")}{s.lo}..{(s.hiCapped ? "≥" : "")}+{s.hi}) | {s.xHi - s.xLo:F0} | {s.Grade} |");
            }
        }
        return text.ToString();
    }

#if UNITY_EDITOR
    // Builds each level scene's controller as Play does, then analyzes them off the main thread and writes the report.
    // Classmate levels are skipped. The scene that was open is reopened.
    public static readonly string[] Classmate = { "PhaseDemo1.1", "PhaseDemo12", "PhaseDemo13", "PhaseDemo14" };
    public static System.Threading.Tasks.Task<string> WriteReport(string path = "Verification/ToleranceReport.md", params string[] levels)
    {
        if (levels.Length == 0) levels = PhaseSandbox.Levels.Except(Classmate).ToArray();
        string open = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        var controllers = new List<(string level, PhaseController controller)>();
        foreach (var level in levels)
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"Assets/Scenes/{level}.unity");
            controllers.Add((level, UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>().CreateController()));
        }
        if (!string.IsNullOrEmpty(open)) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(open);
        return System.Threading.Tasks.Task.Run(() =>
        {
            var results = controllers.Select(l =>
            {
                var result = Analyze(l.level, l.controller);
                UnityEngine.Debug.Log($"{l.level}: {result.Verdict}, {result.steps.Count} steps, tight {result.Count("tight")}, {result.seconds:F0}s");
                return result;
            }).ToList();
            System.IO.File.WriteAllText(path, Markdown(results));
            return string.Join("\n", results.Select(r => $"{r.level}: {r.Verdict}"));
        });
    }
#endif
}
