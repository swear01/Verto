using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Phase;

// Writes Verification/ToleranceReport.md for our levels, or for the levels named on the command line. Run from the project
// root: dotnet run -c Release --project Verification/Tolerance [-- PhaseGear ...]. See Documentation/Tolerance.md.
static class Program
{
    // Each level scene is built from these files by PhaseSandboxBuilder; the classmate level PhaseDemo1.1 is left out. A new level needs a line here.
    static readonly Dictionary<string, string> Maps = new Dictionary<string, string>
    {
        ["PhaseDemo1"] = "Assets/Phase/DemoRooms.json", ["PhaseDemo13"] = "Assets/Phase/Imported/level-a.json",
        ["PhaseDemo14"] = "Assets/Phase/Imported/level-b.json", ["PhaseDemo15"] = "Assets/Phase/Imported/chaos.json",
        ["PhaseUmbrella"] = "Assets/Phase/Imported/umbrella.json", ["PhaseGear"] = "Assets/Phase/Imported/level-gear.json",
    };

    static PhaseController Build(string level)
    {
        string json = File.ReadAllText(Maps[level]);
        var c = level == "PhaseDemo1" ? JObject.Parse(json)["rooms"][0].ToObject<PhaseDemoRoom>().CreateController()
            : PhaseCustomMap.Parse(json).rooms[0].CreateController();
        c.exitRight = true;
        return c;
    }

    // A recorded keyboard route from Verification/Routes, one input per frame.
    static List<PhaseTolerance.Input> Recorded(string level)
    {
        string path = $"Verification/Routes/{level}.json";
        if (!File.Exists(path)) return null;
        return JArray.Parse(File.ReadAllText(path)).SelectMany(s => Enumerable.Repeat(
            new PhaseTolerance.Input { x = (int?)s["x"] ?? 0, held = (bool?)s["jump"] ?? false }, (int)s["frames"])).ToList();
    }

    static void Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var levels = args.Length > 0 ? args : Maps.Keys.ToArray();
        var unknown = levels.Where(level => !Maps.ContainsKey(level)).ToList();
        if (unknown.Count > 0)
        {
            Console.Error.WriteLine($"Unknown level(s): {string.Join(", ", unknown)}. Known: {string.Join(", ", Maps.Keys)}.");
            Environment.Exit(1);
        }
        var results = levels.Select(level =>
        {
            var result = PhaseTolerance.Analyze(level, Build(level), recorded: Recorded(level));
            Console.WriteLine($"{level}: {result.Verdict}, {result.steps.Count} steps, tight {result.Count("tight")}, {result.seconds:F0}s {result.error}");
            return result;
        }).ToList();
        File.WriteAllText("Verification/ToleranceReport.md", PhaseTolerance.Markdown(results));
    }
}
