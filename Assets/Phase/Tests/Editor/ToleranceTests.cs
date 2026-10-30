using System.Linq;
using NUnit.Framework;
using Phase;

public class ToleranceTests
{
    // A floor with a pit of `gap` pixels at x=160; the right edge wins.
    static PhaseController Pit(int gap)
    {
        var c = new PhaseController(new[] { new Block(0, 168, 160, 8), new Block(160 + gap, 168, 160 - gap, 8) }, 24, 157);
        c.exitRight = true;
        return c;
    }

    [Test]
    public void ANarrowPitIsFreeAndANearMaximalOneIsTight()
    {
        var wide = PhaseTolerance.Analyze("wide", Pit(48));
        Assert.That(wide.error, Is.Null);
        Assert.That(wide.jumps, Is.EqualTo(1));
        Assert.That(wide.steps.Single(s => s.kind == PhaseTolerance.Kind.Press).Grade, Is.EqualTo("free"));
        var tight = PhaseTolerance.Analyze("tight", Pit(80));
        Assert.That(tight.error, Is.Null);
        Assert.That(tight.Tightest.Window, Is.LessThanOrEqualTo(PhaseTolerance.TightMax));
        Assert.That(tight.Verdict, Is.EqualTo("too tight"));
    }
}
