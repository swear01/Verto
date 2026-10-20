using System;
using System.Linq;
using NUnit.Framework;
using Phase;
using UnityEngine;

public class KeyBindingTests
{
    string[] before; string saved;
    [SetUp] public void Remember()
    {
        before = PhaseSandbox.Slots?.ToArray();
        saved = PlayerPrefs.HasKey(PhaseSandbox.ControlsKey) ? PlayerPrefs.GetString(PhaseSandbox.ControlsKey) : null;
    }
    [TearDown] public void Restore()
    {
        PhaseSandbox.ApplyControls(before ?? PhaseSandbox.DefaultSlots);
        if (saved == null) PlayerPrefs.DeleteKey(PhaseSandbox.ControlsKey); else PlayerPrefs.SetString(PhaseSandbox.ControlsKey, saved);
        PlayerPrefs.Save();
    }
    static string[] Custom() => new[] { "LeftArrow", "A", "L", "D", "UpArrow", "W", "DownArrow", "S", "J", "Space", "K", "", "H", "Shift" };

    [Test]
    public void InvalidMapsAreRejectedAtomically()
    {
        Assert.That(PhaseSandbox.ApplyControls(Custom()), Is.True);
        foreach (var invalid in new Action<string[]>[] {
            a => a[8] = "L", a => a[2] = "Escape", a => a[2] = a[3] = "", a => a[3] = "L", a => a[11] = "LeftShift",
            a => a[2] = "Delete", a => a[2] = "1", a => a[2] = "Tab", a => a[2] = "LeftCtrl", a => a[2] = "F1", a => a[2] = null
        })
        {
            var candidate = Custom(); invalid(candidate);
            Assert.That(PhaseSandbox.ApplyControls(candidate), Is.False, string.Join(",", candidate));
            Assert.That(PhaseSandbox.Slots, Is.EqualTo(Custom()), "a rejected map leaves the previous one");
        }
        Assert.That(PhaseSandbox.ApplyControls(null), Is.False);
        Assert.That(PhaseSandbox.ApplyControls(Custom().Take(13).ToArray()), Is.False);
    }

    [Test]
    public void SavedBindingsLoadAndInvalidEntriesFallBackToDefaults()
    {
        PhaseSandbox.ApplyControls(Custom()); PhaseSandbox.SaveControls();
        Assert.That(PlayerPrefs.GetString(PhaseSandbox.ControlsKey), Is.EqualTo(string.Join(",", Custom())));
        PhaseSandbox.ApplyControls(PhaseSandbox.DefaultSlots);
        PhaseSandbox.LoadControls();
        Assert.That(PhaseSandbox.Slots, Is.EqualTo(Custom()));
        foreach (string corrupt in new[] { "", "L,L", string.Join(",", Custom()).Replace("J", "L") })
        {
            PhaseSandbox.ApplyControls(Custom());
            PlayerPrefs.SetString(PhaseSandbox.ControlsKey, corrupt); PhaseSandbox.LoadControls();
            Assert.That(PhaseSandbox.Slots, Is.EqualTo(PhaseSandbox.DefaultSlots), corrupt);
        }
        PlayerPrefs.DeleteKey(PhaseSandbox.ControlsKey); PhaseSandbox.LoadControls();
        Assert.That(PhaseSandbox.Slots, Is.EqualTo(PhaseSandbox.DefaultSlots));
    }

    [Test]
    public void EveryBindableKeyHasAKeyCap()
    {
        var texture = new Texture2D(320, 180);
        try
        {
            var names = PhaseKeys.Labels.Keys.ToArray();
            Assert.That(names, Does.Contain("Shift").And.Contain("Space").And.Contain("Digit0").And.Contain("Z").And.Contain("LeftArrow"));
            for (int i = 0; i < names.Length; i += 7)
            {
                var slots = Enumerable.Range(0, 14).Select(s => s % 2 == 0 && i + s / 2 < names.Length ? names[i + s / 2] : "").ToArray();
                for (int s = 0; s < 14; s += 2) if (slots[s] == "") slots[s + 1] = PhaseSandbox.DefaultSlots.First(d => d != "" && !slots.Contains(d));
                Assert.That(PhaseSandbox.ApplyControls(slots), Is.True, string.Join(",", slots));
                var frame = new PixelFrame(texture);
                Assert.DoesNotThrow(() => new PhaseKeys().Draw(frame));
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
}
