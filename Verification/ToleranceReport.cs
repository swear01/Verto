using System.Threading.Tasks;

// Writes Verification/ToleranceReport.md from the connected Editor in Edit Mode. See Documentation/Tolerance.md.
public static class ToleranceReport
{
    public static Task<string> Run() => PhaseTolerance.WriteReport();
    public static Task<string> Smoke() => PhaseTolerance.WriteReport("Temp/tolerance-smoke.md", "PhaseDemo1", "PhaseDemo15");
}
