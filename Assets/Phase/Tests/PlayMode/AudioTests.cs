using System.Collections;
using NUnit.Framework;
using Phase;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class AudioTests
{
    // Editor PlayerPrefs are shared by every open Editor, so pin full volume and restore what the developer saved.
    string savedVolume;
    [SetUp]
    public void FullVolume()
    {
        savedVolume = PlayerPrefs.GetString(PhaseAudio.VolumeKey, null);
        PhaseAudio.SetLevel(true, PhaseAudio.MaxLevel); PhaseAudio.SetLevel(false, PhaseAudio.MaxLevel);
    }
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(GameObject.Find("PHASE audio"));
        if (savedVolume == null) PlayerPrefs.DeleteKey(PhaseAudio.VolumeKey); else PlayerPrefs.SetString(PhaseAudio.VolumeKey, savedVolume);
    }

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
        Assert.IsNotNull(red, "bgm0 is the title's layer");
        PhaseAudio.Title(1f);
        Assert.AreEqual(.1f, red.volume, 1e-4f);
        var title = Object.FindAnyObjectByType<PhaseTitle>();
        Assert.IsNotNull(title);
        var started = red.time;
        title.SelectDemoRoom("1");
        double deadline = Time.realtimeSinceStartupAsDouble + 5;
        while (SceneManager.GetActiveScene().name == PhaseTitle.Scene && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        Assert.AreNotEqual(PhaseTitle.Scene, SceneManager.GetActiveScene().name);
        Assert.IsTrue(red.isPlaying);
        Assert.AreNotEqual(started, red.time);
        Assert.AreEqual(.1f, red.volume, 1e-4f);
        foreach (var m in music) if (m != red) Assert.AreEqual(0f, m.volume, 1e-4f);
        // Leave no ticking scene behind to recreate the audio object.
        var gameplay = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("AudioTestEmpty"));
        yield return SceneManager.UnloadSceneAsync(gameplay);
    }
}
