using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Synthesises the weapon sounds as .wav assets instead of shipping audio files. A gun
    /// report is mostly a burst of filtered noise under a fast exponential decay, which is
    /// cheap to generate and lands close enough to sell the shot — and it keeps the repo free
    /// of binary audio on a machine that has no disk to spare.
    ///
    /// Run from the menu: Game > Bake Weapon Audio
    /// </summary>
    public static class GunAudioBaker
    {
        private const int SampleRate = 44100;
        private const string AudioDir = "Assets/Audio";

        [MenuItem("Game/Bake Weapon Audio")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(AudioDir);

            Write("SFX_Fire",       Rifle());
            Write("SFX_EnemyFire",  EnemyRifle());
            Write("SFX_Reload",     Reload());
            Write("SFX_Empty",      DryFire());

            AssetDatabase.Refresh();
            Debug.Log("<b>Weapon audio baked</b> into Assets/Audio. " +
                      "Rebuild the scene to wire the clips onto the guns.");
        }

        // ------------------------------------------------------------------------- voices

        /// <summary>
        /// Player rifle: a bright crack layered over a low-frequency thump, both decaying
        /// fast. The crack carries the transient, the thump carries the weight.
        /// </summary>
        private static float[] Rifle()
        {
            const float seconds = 0.22f;
            int n = (int)(SampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(1);

            float lowpassState = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;

                // Transient crack: white noise under a very steep decay.
                float crack = (float)(rng.NextDouble() * 2.0 - 1.0)
                            * Mathf.Exp(-t * 55f);

                // Body: the same noise dragged through a one-pole lowpass, decaying slower,
                // which is what gives the report its chest.
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lowpassState += (noise - lowpassState) * 0.06f;
                float body = lowpassState * Mathf.Exp(-t * 18f) * 1.6f;

                // A pitch-dropping sine adds the mechanical "thock" of the action.
                float thump = Mathf.Sin(2f * Mathf.PI * (150f - t * 260f) * t)
                            * Mathf.Exp(-t * 30f) * 0.5f;

                buf[i] = Mathf.Clamp(crack * 0.8f + body + thump, -1f, 1f);
            }

            return buf;
        }

        /// <summary>Enemy rifle: the same shape, pitched down and duller, so the player can
        /// tell incoming fire from their own by ear alone.</summary>
        private static float[] EnemyRifle()
        {
            const float seconds = 0.26f;
            int n = (int)(SampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(2);

            float lowpassState = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;

                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lowpassState += (noise - lowpassState) * 0.03f;   // heavier filtering

                float body  = lowpassState * Mathf.Exp(-t * 14f) * 1.8f;
                float thump = Mathf.Sin(2f * Mathf.PI * (110f - t * 180f) * t)
                            * Mathf.Exp(-t * 24f) * 0.6f;

                buf[i] = Mathf.Clamp(body + thump, -1f, 1f);
            }

            return buf;
        }

        /// <summary>
        /// Reload: four mechanical clicks spaced across the reload window — mag out, mag in,
        /// bolt back, bolt forward. Timing is what makes it read as a reload rather than as
        /// noise.
        /// </summary>
        private static float[] Reload()
        {
            const float seconds = 1.9f;                 // matches WeaponData.reloadSeconds
            int n = (int)(SampleRate * seconds);
            var buf = new float[n];

            // (time, pitch, level) — a heavier clunk for the magazine, sharper for the bolt.
            var clicks = new (float at, float hz, float gain)[]
            {
                (0.06f, 220f, 0.55f),
                (0.55f, 180f, 0.65f),
                (1.15f, 340f, 0.45f),
                (1.55f, 260f, 0.60f),
            };

            var rng = new System.Random(3);

            foreach (var (at, hz, gain) in clicks)
            {
                int start = (int)(at * SampleRate);
                int length = (int)(0.09f * SampleRate);

                for (int i = 0; i < length && start + i < n; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Exp(-t * 60f);

                    float tone  = Mathf.Sin(2f * Mathf.PI * hz * t) * 0.6f;
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.4f;

                    buf[start + i] += (tone + noise) * env * gain;
                }
            }

            for (int i = 0; i < n; i++) buf[i] = Mathf.Clamp(buf[i], -1f, 1f);
            return buf;
        }

        /// <summary>Dry fire: one thin click, no body. The absence of weight is the point.</summary>
        private static float[] DryFire()
        {
            const float seconds = 0.08f;
            int n = (int)(SampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(4);

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 90f);

                float tone  = Mathf.Sin(2f * Mathf.PI * 900f * t) * 0.35f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.5f;

                buf[i] = Mathf.Clamp((tone + noise) * env, -1f, 1f);
            }

            return buf;
        }

        // -------------------------------------------------------------------- wav writing

        private static void Write(string name, float[] samples)
        {
            string path = Path.Combine(AudioDir, name + ".wav");

            using var file = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(file);

            int dataBytes = samples.Length * 2;          // 16-bit mono

            w.Write("RIFF".ToCharArray());
            w.Write(36 + dataBytes);
            w.Write("WAVE".ToCharArray());

            w.Write("fmt ".ToCharArray());
            w.Write(16);                                  // PCM header size
            w.Write((short)1);                            // format: PCM
            w.Write((short)1);                            // channels: mono
            w.Write(SampleRate);
            w.Write(SampleRate * 2);                      // byte rate
            w.Write((short)2);                            // block align
            w.Write((short)16);                           // bits per sample

            w.Write("data".ToCharArray());
            w.Write(dataBytes);

            foreach (float s in samples)
                w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }
    }
}
