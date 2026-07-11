using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Synthesises the jungle soundbed as .wav assets. Every voice here is built from noise
    /// and sine sweeps rather than sampled, which is the only way to get an ambience track
    /// into a repo with 10 GB of disk to spare — a real field recording of a rainforest is
    /// tens of megabytes on its own.
    ///
    /// The layers are deliberately separate files. A single mixed track would loop audibly;
    /// four loops of different lengths played over each other never repeat in the same
    /// alignment twice.
    ///
    /// Run from the menu: Game > Bake Jungle Ambience
    /// </summary>
    public static class AmbienceBaker
    {
        private const int SampleRate = 22050;      // ambience does not need 44.1k; halves size
        private const string AudioDir = "Assets/Audio";

        [MenuItem("Game/Bake Jungle Ambience")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(AudioDir);

            Write("AMB_Insects", Insects(23f));    // prime-ish lengths so the loops drift
            Write("AMB_Birds",   Birds(31f));
            Write("AMB_Wind",    Wind(17f));
            Write("AMB_Water",   Water(13f));
            Write("AMB_Roar",    DistantRoar());

            AssetDatabase.Refresh();
            SetLooping("AMB_Insects", "AMB_Birds", "AMB_Wind", "AMB_Water");

            Debug.Log("<b>Jungle ambience baked</b> into Assets/Audio. " +
                      "Rebuild the island to wire it in.");
        }

        // -------------------------------------------------------------------------- voices

        /// <summary>
        /// Cicadas and crickets: a dense bed of short chirps at scattered pitches. This is
        /// what actually makes a jungle sound like a jungle — it is never silent.
        /// </summary>
        private static float[] Insects(float seconds)
        {
            int n = (int)(SampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(11);

            // A continuous cicada drone under everything, amplitude-modulated so it breathes.
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float drone = Mathf.Sin(2f * Mathf.PI * 4200f * t)
                            * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 7f * t))
                            * 0.018f;
                buf[i] += drone;
            }

            // Individual chirps scattered across the loop.
            int chirps = (int)(seconds * 14);
            for (int c = 0; c < chirps; c++)
            {
                int start = rng.Next(n);
                float pitch = 2600f + (float)rng.NextDouble() * 3200f;
                float gain = 0.05f + (float)rng.NextDouble() * 0.09f;
                int length = (int)(SampleRate * (0.04f + (float)rng.NextDouble() * 0.06f));

                // A cricket chirp is a fast trill, not a tone: the tremolo is the character.
                for (int i = 0; i < length && start + i < n; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Sin(Mathf.PI * (i / (float)length));   // fade in and out
                    float trill = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 55f * t);

                    buf[start + i] += Mathf.Sin(2f * Mathf.PI * pitch * t)
                                    * env * trill * gain;
                }
            }

            return Normalize(buf, 0.55f);
        }

        /// <summary>
        /// Birdsong: short frequency-swept whistles in bursts of two or three, with long
        /// gaps. Sparse on purpose — constant birdsong reads as a ringtone.
        /// </summary>
        private static float[] Birds(float seconds)
        {
            int n = (int)(SampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(12);

            int calls = (int)(seconds * 0.9f);
            for (int c = 0; c < calls; c++)
            {
                int callStart = rng.Next(n);
                int notes = 2 + rng.Next(3);

                float baseHz = 1400f + (float)rng.NextDouble() * 1800f;
                float gain = 0.10f + (float)rng.NextDouble() * 0.14f;

                for (int note = 0; note < notes; note++)
                {
                    int start = callStart + (int)(note * SampleRate * 0.14f);
                    int length = (int)(SampleRate * (0.07f + (float)rng.NextDouble() * 0.08f));

                    // Each note sweeps up or down — that glide is what makes it read as a bird
                    // rather than as a beep.
                    float sweep = ((float)rng.NextDouble() - 0.4f) * 900f;

                    for (int i = 0; i < length && start + i < n && start + i >= 0; i++)
                    {
                        float t = i / (float)SampleRate;
                        float progress = i / (float)length;
                        float hz = baseHz + sweep * progress;

                        float env = Mathf.Sin(Mathf.PI * progress);
                        buf[start + i] += Mathf.Sin(2f * Mathf.PI * hz * t) * env * gain;
                    }
                }
            }

            return Normalize(buf, 0.5f);
        }

        /// <summary>
        /// Wind through leaves: filtered noise whose brightness and level swell and fall.
        /// Two lowpass poles in series, because one is too harsh to sit under everything else.
        /// </summary>
        private static float[] Wind(float seconds)
        {
            int n = (int)(SampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(13);

            float p1 = 0f, p2 = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

                // Gusts: two slow LFOs at unrelated rates, so the swell never settles into a
                // rhythm the ear can predict.
                float gust = 0.45f
                           + 0.35f * Mathf.Sin(2f * Mathf.PI * 0.11f * t)
                           + 0.20f * Mathf.Sin(2f * Mathf.PI * 0.29f * t + 1.3f);

                // Cutoff rides the gust — a stronger gust is a brighter rustle.
                float cutoff = 0.05f + gust * 0.05f;
                p1 += (noise - p1) * cutoff;
                p2 += (p1 - p2) * cutoff;

                buf[i] = p2 * gust * 0.5f;
            }

            return Normalize(buf, 0.42f);
        }

        /// <summary>
        /// Running water: bright filtered noise with a faster, shallower modulation than the
        /// wind, plus occasional bubbles.
        /// </summary>
        private static float[] Water(float seconds)
        {
            int n = (int)(SampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(14);

            float hp = 0f, prev = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

                // Highpass: water is all treble. A one-pole highpass is noise minus its own
                // lowpass.
                hp += (noise - hp) * 0.35f;
                float bright = noise - hp;

                float flow = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.7f * t);
                buf[i] = bright * flow * 0.35f;

                prev = bright;
            }

            // Bubbles: short rising sine blips.
            int bubbles = (int)(seconds * 6);
            for (int b = 0; b < bubbles; b++)
            {
                int start = rng.Next(n);
                int length = (int)(SampleRate * 0.05f);
                float hz = 500f + (float)rng.NextDouble() * 700f;

                for (int i = 0; i < length && start + i < n; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Exp(-t * 40f);
                    buf[start + i] += Mathf.Sin(2f * Mathf.PI * (hz + t * 800f) * t)
                                    * env * 0.08f;
                }
            }

            return Normalize(buf, 0.45f);
        }

        /// <summary>
        /// Something large, far away, that you would rather not meet. A low growl under a
        /// slow decay. Played rarely and at random by AmbienceController — a roar you can
        /// predict is not a threat.
        /// </summary>
        private static float[] DistantRoar()
        {
            const float seconds = 2.4f;
            int n = (int)(SampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(15);

            float lp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;

                // Body: a growl is a low tone plus its harmonics, wavering.
                float waver = 1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 5.5f * t);
                float fundamental = 62f * waver;

                float tone = Mathf.Sin(2f * Mathf.PI * fundamental * t) * 0.6f
                           + Mathf.Sin(2f * Mathf.PI * fundamental * 2f * t) * 0.25f
                           + Mathf.Sin(2f * Mathf.PI * fundamental * 3f * t) * 0.12f;

                // Breath: heavily filtered noise riding along with it.
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.02f;

                // Swell in, hold, fall away — distance is mostly in the envelope.
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / seconds));
                env *= env;

                buf[i] = (tone * 0.7f + lp * 0.5f) * env * 0.5f;
            }

            return Normalize(buf, 0.6f);
        }

        // --------------------------------------------------------------------------- utils

        /// <summary>Scales the buffer so its loudest sample sits at `peak`. Without this the
        /// layers fight each other and the mix clips.</summary>
        private static float[] Normalize(float[] buf, float peak)
        {
            float max = 0f;
            foreach (float s in buf) max = Mathf.Max(max, Mathf.Abs(s));
            if (max < 1e-6f) return buf;

            float scale = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= scale;
            return buf;
        }

        /// <summary>
        /// Crossfades the tail into the head so the loop point is inaudible. A raw generated
        /// buffer clicks every time it wraps.
        /// </summary>
        private static float[] LoopSeam(float[] buf, float seconds)
        {
            int fade = Mathf.Min((int)(SampleRate * seconds), buf.Length / 4);

            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                int tail = buf.Length - fade + i;
                buf[i] = Mathf.Lerp(buf[tail], buf[i], t);
            }

            return buf;
        }

        private static void Write(string name, float[] samples)
        {
            // Everything except the roar is a loop, and everything that loops needs its seam
            // hidden.
            if (name != "AMB_Roar") samples = LoopSeam(samples, 0.35f);

            string path = Path.Combine(AudioDir, name + ".wav");

            using var file = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(file);

            int dataBytes = samples.Length * 2;

            w.Write("RIFF".ToCharArray());
            w.Write(36 + dataBytes);
            w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray());
            w.Write(16);
            w.Write((short)1);                       // PCM
            w.Write((short)1);                       // mono
            w.Write(SampleRate);
            w.Write(SampleRate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write("data".ToCharArray());
            w.Write(dataBytes);

            foreach (float s in samples)
                w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        }

        /// <summary>
        /// Marks the beds as looping and streams them from disk. Loading a 30-second clip
        /// decompressed into memory would cost more RAM than the sound is worth on a phone.
        /// </summary>
        private static void SetLooping(params string[] names)
        {
            foreach (string name in names)
            {
                string path = $"{AudioDir}/{name}.wav";
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.55f;

                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }
    }
}
