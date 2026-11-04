using System.Collections;
using NUnit.Framework;
using Phase;
using UnityEngine;
using UnityEngine.SceneManagement;
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

    [UnityTest]
    public IEnumerator TitlePlaysMusicAndLevelOneKeepsIt()
    {
        yield return SceneManager.LoadSceneAsync(PhaseTitle.Scene);
        for (int i = 0; i < 40; i++) yield return null;
        var music = System.Array.FindAll(Object.FindObjectsByType<AudioSource>(), s => s.loop);
        Assert.AreEqual(3, music.Length);
        foreach (var m in music) Assert.IsTrue(m.isPlaying);
        var red = System.Array.Find(music, m => m.clip.name == "bgm0");
        Assert.Greater(red.volume, 0f);
        var started = red.time;
        Object.FindAnyObjectByType<PhaseTitle>().SelectDemoRoom("1");
        double deadline = Time.realtimeSinceStartupAsDouble + 5;
        while (SceneManager.GetActiveScene().name == PhaseTitle.Scene && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        Assert.IsTrue(red.isPlaying);
        Assert.AreNotEqual(PhaseTitle.Scene, SceneManager.GetActiveScene().name);
        Assert.AreNotEqual(started, red.time);
    }
}
