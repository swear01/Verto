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
    public void ANarrowPitIsFreeAndANearMaximalTakeoffIsTight()
    {
        var wide = PhaseTolerance.Analyze("wide", Pit(48));
        Assert.That(wide.error, Is.Null);
        Assert.That(wide.jumps, Is.EqualTo(1));
        var presses = wide.steps.Where(s => s.kind == PhaseTolerance.Kind.Press).ToList();
        Assert.That(presses, Is.Not.Empty);
        Assert.That(presses.Select(s => s.Grade), Is.All.EqualTo("free"));
        // The takeoff barely reaches the far wall, and a wall jump finishes the climb.
        var tight = PhaseTolerance.Analyze("tight", Pit(84));
        Assert.That(tight.error, Is.Null);
        Assert.That(tight.steps.FirstOrDefault(s => s.jumps)?.Grade, Is.EqualTo("tight"));
        Assert.That(tight.Verdict, Is.EqualTo("too tight"));
    }
}
