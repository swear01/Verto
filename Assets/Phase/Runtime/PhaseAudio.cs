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

        static void Init()
        {
            if (source != null) return;
            var go = new GameObject("PHASE audio") { hideFlags = HideFlags.HideAndDontSave };
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
        }

        static void Play(AudioClip clip) { if (clip != null) source.PlayOneShot(clip); }

        // Compares controller state across one step and plays what changed.
        public static void Step(PhaseController c, (int jumps, int deaths, bool grounded, int airJump) was)
        {
            Init();
            var p = c.player;
            if (c.deaths > was.deaths) Play(death);
            else if (c.jumps > was.jumps) Play(jump[Math.Max(0, c.phase) % 3]);
            else if (p.grounded && !was.grounded && c.respawn <= 0) Play(land);
            if (p.airJump > was.airJump) Play(crystal);
        }
    }
}
