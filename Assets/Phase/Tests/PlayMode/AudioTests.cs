using System.Collections;
using NUnit.Framework;
using Phase;
using UnityEngine;
using UnityEngine.TestTools;

public class AudioTests
{
    [TearDown]
    public void TearDown() { Object.DestroyImmediate(GameObject.Find("PHASE audio")); }

    [UnityTest]
    public IEnumerator MusicLoopsAndFollowsPhase()
    {
        var c = new PhaseController(new Block[0], 32, 100);
        var was = (c.jumps, c.deaths, c.player.grounded, c.player.airJump);
        PhaseAudio.Step(c, was);
        var music = System.Array.FindAll(Object.FindObjectsByType<AudioSource>(), s => s.loop);
        System.Array.Sort(music, (a, b) => string.CompareOrdinal(a.clip.name, b.clip.name));
        Assert.AreEqual(3, music.Length);
        foreach (var m in music) { Assert.IsTrue(m.isPlaying); Assert.Greater(m.clip.length, 5f); }
        Assert.AreEqual(music[0].clip.samples, music[1].clip.samples);
        Assert.AreEqual(music[0].clip.samples, music[2].clip.samples);
        for (int i = 0; i < 90; i++) PhaseAudio.Step(c, was);
        Assert.AreEqual(.1f, music[Mathf.Max(0, c.phase) % 3].volume, 1e-4f);
        c.phase = 1;
        for (int i = 0; i < 90; i++) PhaseAudio.Step(c, was);
        Assert.AreEqual(.1f, music[1].volume, 1e-4f);
        Assert.AreEqual(0f, music[0].volume, 1e-4f);
        yield return null;
    }
}
