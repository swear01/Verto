using System;
using UnityEngine;

namespace Phase
{
    // Short synth blips generated at startup; no audio assets.
    public static class PhaseAudio
    {
        const int Rate = 44100;
        static AudioSource source;
        static AudioClip[] jump;
        static AudioClip land, death, crystal;
        static AudioSource[] music;
        static readonly int[] Bass = { 45, 41, 48, 43 };
        static readonly int[][] Chord = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 60, 64, 67 }, new[] { 55, 59, 62 } };
        static readonly int[] Lead = { 76, 0, 72, 0, 69, 0, 72, 76, 77, 0, 74, 0, 72, 0, 69, 72, 76, 0, 72, 0, 67, 0, 72, 76, 74, 0, 71, 0, 67, 0, 71, 74 };

        static AudioClip Tone(string name, float seconds, Func<float, float> pitch, float volume, bool square = true, float noise = 0)
        {
            int n = (int)(Rate * seconds); var data = new float[n]; double phase = 0;
            var random = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n; phase += pitch(t) / Rate;
                float wave = square ? (phase % 1 < .5 ? 1 : -1) : (float)Math.Sin(phase * 2 * Math.PI);
                wave = wave * (1 - noise) + ((float)random.NextDouble() * 2 - 1) * noise;
                data[i] = wave * volume * (1 - t) * Mathf.Min(1, i / 60f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false); clip.SetData(data, 0); return clip;
        }

        static float Hz(int midi) { return 440f * Mathf.Pow(2, (midi - 69) / 12f); }

        // Four bars of Am-F-C-G; every layer carries the bass, plus hats (red), arpeggio (blue) or lead (green).
        static AudioClip Loop(int layer)
        {
            const float Beat = 60f / 128 / 2; int per = (int)(Rate * Beat), n = per * 32;
            var data = new float[n]; var random = new System.Random(layer);
            for (int i = 0; i < 32; i++)
            {
                int bar = i / 8, s = i % 8, at = i * per;
                float bass = Hz(Bass[bar]), chord = layer == 1 ? Hz(Chord[bar][s % 3] + 12) : 0, lead = layer == 2 && Lead[i] > 0 ? Hz(Lead[i]) : 0;
                for (int k = 0; k < per; k++)
                {
                    float t = k / (float)per, e = Mathf.Min(1, k / 40f) * (1 - t), v = 0;
                    if (s == 0 || s == 3 || s == 4 || s == 6) v += (Mathf.Repeat(bass * k / Rate, 1) < .5f ? 1 : -1) * .5f * e;
                    if (layer == 0 && s % 2 == 1) v += ((float)random.NextDouble() * 2 - 1) * .25f * (1 - t) * (1 - t);
                    if (layer == 1) v += (Mathf.Repeat(chord * k / Rate, 1) < .25f ? 1 : -1) * .3f * e;
                    if (layer == 2 && Lead[i] > 0) v += Mathf.Sin(2 * Mathf.PI * lead * k / Rate) * .45f * e;
                    data[at + k] = v;
                }
            }
            var clip = AudioClip.Create("bgm" + layer, n, 1, Rate, false); clip.SetData(data, 0); return clip;
        }

        static void Init()
        {
            if (source != null) return;
            var go = new GameObject("PHASE audio") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            // PHASE scenes carry no listener; this persistent one hears every room.
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null) go.AddComponent<AudioListener>();
            // One rising blip per phase colour, so each jump also announces the new phase.
            jump = new AudioClip[3];
            for (int p = 0; p < 3; p++) { float f = 520 + 130 * p; jump[p] = Tone("jump" + p, .09f, t => f + 500 * t, .12f); }
            land = Tone("land", .05f, t => 140 - 60 * t, .1f, false, .5f);
            death = Tone("death", .32f, t => 420 - 360 * t, .14f, true, .35f);
            crystal = Tone("crystal", .18f, t => t < .5f ? 990 : 1320, .1f, false);
            music = new AudioSource[3];
            for (int i = 0; i < 3; i++)
            {
                music[i] = go.AddComponent<AudioSource>();
                music[i].clip = Loop(i); music[i].loop = true; music[i].volume = 0;
            }
            // Start together so the layers stay in step; Unity Web only reaches here after the first key press.
            foreach (var m in music) m.Play();
        }

        static void Play(AudioClip clip) { if (clip != null) source.PlayOneShot(clip); }

        // Compares controller state across one step and plays what changed.
        public static void Step(PhaseController c, (int jumps, int deaths, bool grounded, int airJump) was)
        {
            Init();
            var p = c.player;
            // Each jump moves the music to that phase's layer.
            const float MusicVolume = .1f, MusicFadeSeconds = .5f;
            for (int i = 0; i < 3; i++)
                music[i].volume = Mathf.MoveTowards(music[i].volume, i == Math.Max(0, c.phase) % 3 ? MusicVolume : 0, MusicVolume / MusicFadeSeconds * (float)PhaseController.Dt);
            if (c.deaths > was.deaths) Play(death);
            else if (c.jumps > was.jumps) Play(jump[Math.Max(0, c.phase) % 3]);
            else if (p.grounded && !was.grounded && c.respawn <= 0) Play(land);
            if (p.airJump > was.airJump) Play(crystal);
        }
    }
}
