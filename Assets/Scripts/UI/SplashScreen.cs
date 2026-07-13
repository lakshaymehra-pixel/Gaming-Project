using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Cinematic war-themed splash screen with synthesised audio:
    /// distant gunfire, jungle insects, a heartbeat, heavy breathing,
    /// and a rising tension drone. The screen fades through a dark
    /// jungle palette with blood-red accents.
    /// </summary>
    public class SplashScreen : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float logoFadeIn     = 1.5f;
        [SerializeField] private float logoHold       = 2.0f;
        [SerializeField] private float logoFadeOut    = 1.0f;
        [SerializeField] private float titleDelay     = 0.5f;
        [SerializeField] private float titleFadeIn    = 1.0f;
        [SerializeField] private float subtitleDelay  = 0.8f;
        [SerializeField] private string gameSceneName = "Island";

        // Built at runtime
        private Canvas        canvas;
        private CanvasGroup   logoGroup;
        private CanvasGroup   titleGroup;
        private CanvasGroup   tapGroup;
        private CanvasGroup   warningGroup;
        private Image         bgImage;
        private Image         vignetteImage;
        private Image         bloodStreakImage;
        private TMP_Text      titleTMP;
        private bool          readyToPlay;

        // Audio — assigned by SplashSceneBuilder if real clips exist,
        // otherwise synthesised at runtime.
        [Header("Audio Clips (optional — leave empty for synth fallback)")]
        [SerializeField] private AudioClip clipHeartbeat;
        [SerializeField] private AudioClip clipBreathing;
        [SerializeField] private AudioClip clipJungle;
        [SerializeField] private AudioClip clipJungleAnimals;
        [SerializeField] private AudioClip clipGunfire;
        [SerializeField] private AudioClip clipGunShot;
        [SerializeField] private AudioClip clipGunReload;
        [SerializeField] private AudioClip clipDrone;
        [SerializeField] private AudioClip clipThunder;
        [SerializeField] private AudioClip clipSea;

        private AudioSource   heartbeatSource;
        private AudioSource   breathingSource;
        private AudioSource   jungleSource;
        private AudioSource   jungleAnimalsSource;
        private AudioSource   gunfireSource;
        private AudioSource   gunShotSource;
        private AudioSource   gunReloadSource;
        private AudioSource   droneSource;
        private AudioSource   thunderSource;
        private AudioSource   seaSource;

        private const int SampleRate = 44100;

        private void Start()
        {
            // If no AudioListener in scene, add one to this object
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                gameObject.AddComponent<AudioListener>();
                Debug.LogWarning("[Splash] No AudioListener found — added one.");
            }

            BuildUI();
            BuildAudio();

            // Debug: log audio status
            Debug.Log($"[Splash] Clips wired: " +
                $"Heart={clipHeartbeat != null} Jungle={clipJungle != null} " +
                $"Gun={clipGunfire != null} Thunder={clipThunder != null} " +
                $"Sea={clipSea != null} GunShot={clipGunShot != null}");
            Debug.Log($"[Splash] Sources: " +
                $"Heart={heartbeatSource?.clip != null} Jungle={jungleSource?.clip != null} " +
                $"Thunder={thunderSource?.clip != null}");

            StartCoroutine(PlaySequence());
        }

        private void Update()
        {
            if (readyToPlay && (Input.anyKeyDown || IsTouchDown()))
            {
                readyToPlay = false;
                StartCoroutine(TransitionToGame());
            }

            // Very subtle vignette pulse
            if (vignetteImage != null)
            {
                float pulse = Mathf.Abs(Mathf.Sin(Time.time * 1.2f * Mathf.PI));
                var c = vignetteImage.color;
                c.a = 0.05f + pulse * 0.05f;
                vignetteImage.color = c;
            }
        }

        private static bool IsTouchDown()
        {
            return Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
        }

        // ───────────────────── animation sequence ─────────────────────

        private IEnumerator PlaySequence()
        {
            logoGroup.alpha    = 0f;
            titleGroup.alpha   = 0f;
            tapGroup.alpha     = 0f;
            warningGroup.alpha = 0f;

            // ═══════════════════════════════════════════════════════════
            // PHASE 1: Silence → Sea waves only (peaceful start)
            // ═══════════════════════════════════════════════════════════
            if (seaSource != null)
            {
                seaSource.volume = 0.6f;
                seaSource.Play();
            }

            yield return new WaitForSeconds(2.0f);

            // ═══════════════════════════════════════════════════════════
            // PHASE 2: Jungle sounds fade in slowly (nature waking up)
            // ═══════════════════════════════════════════════════════════
            jungleSource.volume = 0f;
            jungleSource.Play();
            yield return FadeAudioVolume(jungleSource, 0f, 0.7f, 2.0f);

            // Sea goes quieter as jungle takes over
            if (seaSource != null)
                StartCoroutine(FadeAudioVolume(seaSource, 0.6f, 0.2f, 1.5f));

            yield return new WaitForSeconds(1.0f);

            // Animals join in (separate layer, not mixed on top)
            if (jungleAnimalsSource != null)
            {
                jungleAnimalsSource.volume = 0f;
                jungleAnimalsSource.Play();
                StartCoroutine(FadeAudioVolume(jungleAnimalsSource, 0f, 0.5f, 1.5f));
            }

            yield return new WaitForSeconds(2.0f);

            // ═══════════════════════════════════════════════════════════
            // PHASE 3: Something breaks the peace — warning
            // ═══════════════════════════════════════════════════════════

            // Everything goes quiet suddenly
            StartCoroutine(FadeAudioVolume(jungleSource, jungleSource.volume, 0.15f, 0.5f));
            if (jungleAnimalsSource != null)
                StartCoroutine(FadeAudioVolume(jungleAnimalsSource, jungleAnimalsSource.volume, 0f, 0.5f));
            if (seaSource != null)
                StartCoroutine(FadeAudioVolume(seaSource, seaSource.volume, 0.05f, 0.5f));

            yield return new WaitForSeconds(0.8f);

            // "THE ISLAND REMEMBERS..." fades in during silence
            yield return Fade(warningGroup, 0f, 1f, 1.5f);
            yield return new WaitForSeconds(1.5f);

            // Gun reload — ONLY this sound, nothing else
            PlayIfExists(gunReloadSource);
            yield return new WaitForSeconds(1.0f);

            yield return Fade(warningGroup, 1f, 0f, 1.0f);
            yield return new WaitForSeconds(0.5f);

            // ═══════════════════════════════════════════════════════════
            // PHASE 4: YAARI logo with heartbeat only
            // ═══════════════════════════════════════════════════════════
            heartbeatSource.volume = 0.9f;
            heartbeatSource.Play();

            yield return Fade(logoGroup, 0f, 1f, logoFadeIn);
            yield return new WaitForSeconds(logoHold);
            yield return Fade(logoGroup, 1f, 0f, logoFadeOut);

            // Stop heartbeat
            StartCoroutine(FadeAudio(heartbeatSource, 0.5f));
            yield return new WaitForSeconds(0.3f);

            // ═══════════════════════════════════════════════════════════
            // PHASE 5: Title SLAM — single gunshot impact
            // ═══════════════════════════════════════════════════════════

            // SINGLE GUNSHOT + Title appears at same moment
            PlayIfExists(gunShotSource);
            yield return Fade(titleGroup, 0f, 1f, 0.1f);
            StartCoroutine(ScreenShake(titleTMP.rectTransform, 0.3f, 10f));

            yield return new WaitForSeconds(1.0f);

            // ═══════════════════════════════════════════════════════════
            // PHASE 6: Gunfire burst (war atmosphere, short)
            // ═══════════════════════════════════════════════════════════
            gunfireSource.volume = 0.8f;
            gunfireSource.Play();

            yield return new WaitForSeconds(2.0f);

            // Gunfire fades, jungle comes back softly
            StartCoroutine(FadeAudio(gunfireSource, 1.5f));
            jungleSource.volume = 0f;
            jungleSource.Play();
            StartCoroutine(FadeAudioVolume(jungleSource, 0f, 0.4f, 2.0f));

            yield return new WaitForSeconds(1.5f);

            // ═══════════════════════════════════════════════════════════
            // PHASE 7: Ready — calm jungle, tap to deploy
            // ═══════════════════════════════════════════════════════════
            readyToPlay = true;
            StartCoroutine(PulseTap());
        }

        private static IEnumerator FadeAudioVolume(AudioSource src, float from, float to, float dur)
        {
            if (src == null) yield break;
            float t = 0f;
            src.volume = from;
            while (t < dur)
            {
                t += Time.deltaTime;
                src.volume = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            src.volume = to;
        }

        private static void PlayIfExists(AudioSource src)
        {
            if (src != null) src.Play();
        }

        private IEnumerator PulseTap()
        {
            while (readyToPlay)
            {
                yield return Fade(tapGroup, 0f, 1f, 0.8f);
                yield return Fade(tapGroup, 1f, 0.3f, 0.8f);
            }
        }

        private IEnumerator FlashBloodStreaks()
        {
            for (int i = 0; i < 3; i++)
            {
                var c = bloodStreakImage.color;
                c.a = 0.6f;
                bloodStreakImage.color = c;
                yield return new WaitForSeconds(0.08f);
                c.a = 0f;
                bloodStreakImage.color = c;
                yield return new WaitForSeconds(0.15f);
            }
        }

        private IEnumerator ScreenShake(RectTransform rt, float duration, float magnitude)
        {
            Vector2 original = rt.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;
                rt.anchoredPosition = original + new Vector2(x, y);
                t += Time.deltaTime;
                yield return null;
            }
            rt.anchoredPosition = original;
        }

        private IEnumerator TransitionToGame()
        {
            // Single gunshot on tap
            PlayIfExists(gunShotSource);

            // Fade everything out
            StartCoroutine(FadeAudio(jungleSource, 0.5f));
            StartCoroutine(FadeAudio(jungleAnimalsSource, 0.5f));
            StartCoroutine(FadeAudio(seaSource, 0.5f));
            StartCoroutine(FadeAudio(heartbeatSource, 0.3f));
            StartCoroutine(FadeAudio(breathingSource, 0.3f));
            StartCoroutine(FadeAudio(droneSource, 0.3f));

            yield return new WaitForSeconds(0.3f);

            // Fade to black
            bgImage.color = Color.black;
            yield return FadeImage(bgImage, 0f, 1f, 0.6f);

            SceneManager.LoadScene(gameSceneName);
        }

        private static IEnumerator FadeAudio(AudioSource src, float duration)
        {
            if (src == null || !src.isPlaying) yield break;
            float startVol = src.volume;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                src.volume = Mathf.Lerp(startVol, 0f, t / duration);
                yield return null;
            }
            src.Stop();
            src.volume = startVol;
        }

        // ───────────────────── helpers ─────────────────────

        private static IEnumerator Fade(CanvasGroup cg, float from, float to, float dur)
        {
            float t = 0f;
            cg.alpha = from;
            while (t < dur)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            cg.alpha = to;
        }

        private static IEnumerator FadeImage(Image img, float fromA, float toA, float dur)
        {
            float t = 0f;
            Color c = img.color;
            c.a = fromA;
            img.color = c;
            while (t < dur)
            {
                t += Time.deltaTime;
                c.a = Mathf.Lerp(fromA, toA, t / dur);
                img.color = c;
                yield return null;
            }
            c.a = toA;
            img.color = c;
        }

        // ───────────────────── audio synthesis ─────────────────────

        private void BuildAudio()
        {
            // Use real clips if assigned by the scene builder, else synthesise.
            heartbeatSource    = ClipOrSynth(clipHeartbeat, "Heartbeat", SynthHeartbeat(), true, 1.0f);
            breathingSource    = ClipOrSynth(clipBreathing, "Breathing", SynthBreathing(), true, 0.85f);
            jungleSource       = ClipOrSynth(clipJungle,    "Jungle",    SynthJungleNight(), true, 0.65f);
            jungleAnimalsSource = ClipOrNull(clipJungleAnimals, "JungleAnimals", true, 0.5f);
            gunfireSource      = ClipOrSynth(clipGunfire,   "Gunfire",   SynthDistantGunfire(), false, 0.9f);
            gunShotSource      = ClipOrNull(clipGunShot,    "GunShot",    false, 0.8f);
            gunReloadSource    = ClipOrNull(clipGunReload,  "GunReload",  false, 0.7f);
            droneSource        = ClipOrSynth(clipDrone,     "Drone",     SynthTensionDrone(), true, 0.6f);
            thunderSource      = ClipOrSynth(clipThunder,   "Thunder",   SynthThunder(), false, 1.0f);
            seaSource          = ClipOrNull(clipSea,        "Sea",        true, 0.4f);
        }

        /// <summary>Returns an AudioSource only if the real clip exists, null otherwise.</summary>
        private AudioSource ClipOrNull(AudioClip clip, string name, bool loop, float vol)
        {
            if (clip == null) return null;
            var go = new GameObject("SFX_" + name);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = loop;
            src.volume = vol;
            src.playOnAwake = false;
            return src;
        }

        private AudioSource ClipOrSynth(AudioClip real, string name, float[] fallback,
            bool loop, float vol)
        {
            if (real != null)
            {
                var go = new GameObject("SFX_" + name);
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.clip = real;
                src.loop = loop;
                src.volume = vol;
                src.playOnAwake = false;
                return src;
            }
            return MakeSource(name, fallback, loop, vol);
        }

        private AudioSource MakeSource(string name, float[] samples, bool loop, float vol)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);

            var go = new GameObject("SFX_" + name);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = loop;
            src.volume = vol;
            src.playOnAwake = false;
            return src;
        }

        /// <summary>
        /// Heartbeat: two low thumps close together (lub-dub), repeating.
        /// The primal fear sound — your own body telling you something is wrong.
        /// </summary>
        private float[] SynthHeartbeat()
        {
            // ~72 BPM heartbeat, 8 seconds loop
            const float seconds = 8f;
            int n = (int)(SampleRate * seconds);
            var buf = new float[n];

            float beatInterval = 0.83f; // seconds between beats (~72 BPM)

            for (float beatTime = 0f; beatTime < seconds; beatTime += beatInterval)
            {
                // Lub (first thump - louder, lower)
                AddThump(buf, beatTime, 45f, 0.12f, 0.9f);
                // Dub (second thump - softer, higher, 0.18s later)
                AddThump(buf, beatTime + 0.18f, 65f, 0.08f, 0.55f);
            }

            return Normalize(buf, 0.95f);
        }

        private void AddThump(float[] buf, float startSec, float hz, float duration, float gain)
        {
            int start = (int)(startSec * SampleRate);
            int len = (int)(duration * SampleRate);

            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 25f) * Mathf.Sin(Mathf.PI * (i / (float)len));
                float tone = Mathf.Sin(2f * Mathf.PI * hz * t);
                // Add sub-harmonic for chest feel
                tone += Mathf.Sin(2f * Mathf.PI * hz * 0.5f * t) * 0.5f;
                buf[start + i] += tone * env * gain;
            }
        }

        /// <summary>
        /// Heavy breathing: filtered noise shaped into inhale-exhale cycles.
        /// Faster than normal — panicked breathing in the dark.
        /// </summary>
        private float[] SynthBreathing()
        {
            const float seconds = 6f;
            int n = (int)(SampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(42);

            float lp = 0f;
            float breathRate = 2.2f; // breaths per second (panicked)

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;

                // Breath cycle: inhale rises, exhale falls
                float cycle = Mathf.Sin(2f * Mathf.PI * breathRate * t);
                float envelope = Mathf.Abs(cycle);
                envelope = Mathf.Pow(envelope, 0.7f); // sharper attacks

                // Filtered noise = breath sound
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                // Different filter for inhale vs exhale
                float cutoff = cycle > 0 ? 0.08f : 0.04f; // inhale brighter
                lp += (noise - lp) * cutoff;

                // Add slight whistle on inhale (nasal resonance)
                float whistle = 0f;
                if (cycle > 0.3f)
                    whistle = Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.03f * (cycle - 0.3f);

                buf[i] = (lp + whistle) * envelope * 0.6f;
            }

            return Normalize(buf, 0.5f);
        }

        /// <summary>
        /// Night jungle: dense insect chirps, occasional eerie bird call,
        /// and an unsettling low hum. Darker and more threatening than the
        /// in-game ambience.
        /// </summary>
        private float[] SynthJungleNight()
        {
            const float seconds = 12f;
            int n = (int)(SampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(66);

            // Dense high-pitched insect drone
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float drone = Mathf.Sin(2f * Mathf.PI * 4800f * t)
                            * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 9f * t))
                            * 0.015f;
                // Second insect at different pitch
                drone += Mathf.Sin(2f * Mathf.PI * 3600f * t)
                       * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 6f * t))
                       * 0.012f;
                buf[i] += drone;
            }

            // Scattered chirps (more aggressive, closer sounding)
            int chirps = (int)(seconds * 20);
            for (int c = 0; c < chirps; c++)
            {
                int start = rng.Next(n);
                float pitch = 3000f + (float)rng.NextDouble() * 4000f;
                float gain = 0.06f + (float)rng.NextDouble() * 0.1f;
                int length = (int)(SampleRate * (0.02f + (float)rng.NextDouble() * 0.04f));

                for (int i = 0; i < length && start + i < n; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Mathf.Sin(Mathf.PI * (i / (float)length));
                    float trill = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 70f * t);
                    buf[start + i] += Mathf.Sin(2f * Mathf.PI * pitch * t) * env * trill * gain;
                }
            }

            // Eerie bird calls (slower, lower, creepier)
            for (int b = 0; b < 4; b++)
            {
                int start = rng.Next(n / 2) + n / 4;
                float hz = 800f + (float)rng.NextDouble() * 600f;
                int length = (int)(SampleRate * 0.4f);
                float sweep = -400f; // downward sweep = eerie

                for (int i = 0; i < length && start + i < n; i++)
                {
                    float t = i / (float)SampleRate;
                    float progress = i / (float)length;
                    float freq = hz + sweep * progress;
                    float env = Mathf.Sin(Mathf.PI * progress) * Mathf.Exp(-progress * 2f);
                    buf[start + i] += Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.12f;
                }
            }

            // Low ominous hum (sub bass rumble)
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float hum = Mathf.Sin(2f * Mathf.PI * 38f * t) * 0.08f;
                hum += Mathf.Sin(2f * Mathf.PI * 57f * t) * 0.04f;
                float swell = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.08f * t);
                buf[i] += hum * swell;
            }

            return Normalize(buf, 0.55f);
        }

        /// <summary>
        /// Distant gunfire: bursts of muffled shots with echoes.
        /// Sounds like a firefight happening far away in the jungle.
        /// </summary>
        private float[] SynthDistantGunfire()
        {
            const float seconds = 4f;
            int n = (int)(SampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(77);

            // Multiple bursts of shots
            int bursts = 3 + rng.Next(3);
            float time = 0.1f;

            for (int b = 0; b < bursts; b++)
            {
                int shots = 3 + rng.Next(6);
                float gap = 0.07f + (float)rng.NextDouble() * 0.05f;

                for (int s = 0; s < shots; s++)
                {
                    float shotTime = time + s * gap;
                    int start = (int)(shotTime * SampleRate);
                    if (start >= n) break;

                    // Each shot: muffled crack + echo
                    float shotLp = 0f;
                    int shotLen = (int)(SampleRate * 0.15f);
                    float gain = 0.3f + (float)rng.NextDouble() * 0.3f;

                    for (int i = 0; i < shotLen && start + i < n; i++)
                    {
                        float t = i / (float)SampleRate;
                        float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                        shotLp += (noise - shotLp) * 0.03f; // heavy lowpass = distant
                        float env = Mathf.Exp(-t * 20f);
                        float thump = Mathf.Sin(2f * Mathf.PI * 80f * t) * Mathf.Exp(-t * 15f);
                        buf[start + i] += (shotLp + thump * 0.5f) * env * gain;
                    }

                    // Echo (delayed, quieter copy)
                    int echoDelay = (int)(SampleRate * (0.3f + (float)rng.NextDouble() * 0.2f));
                    int echoStart = start + echoDelay;
                    float echoLp = 0f;
                    for (int i = 0; i < shotLen / 2 && echoStart + i < n; i++)
                    {
                        float t = i / (float)SampleRate;
                        float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                        echoLp += (noise - echoLp) * 0.015f;
                        float env = Mathf.Exp(-t * 25f);
                        buf[echoStart + i] += echoLp * env * gain * 0.3f;
                    }
                }

                time += shots * gap + 0.4f + (float)rng.NextDouble() * 0.6f;
            }

            return Normalize(buf, 0.6f);
        }

        /// <summary>
        /// Rising tension drone: a low frequency sweep that builds unease.
        /// The kind of sound that makes your skin crawl.
        /// </summary>
        private float[] SynthTensionDrone()
        {
            const float seconds = 10f;
            int n = (int)(SampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(99);

            float lp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)n;

                // Rising pitch builds tension
                float hz = 30f + progress * 50f;

                // Dissonant intervals
                float tone = Mathf.Sin(2f * Mathf.PI * hz * t) * 0.4f;
                tone += Mathf.Sin(2f * Mathf.PI * hz * 1.5f * t) * 0.2f; // fifth
                tone += Mathf.Sin(2f * Mathf.PI * hz * 1.414f * t) * 0.15f; // tritone (devil's interval)

                // Filtered noise bed rising with it
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float cutoff = 0.01f + progress * 0.03f;
                lp += (noise - lp) * cutoff;

                // Volume swells up over time
                float vol = 0.2f + progress * 0.8f;

                buf[i] = (tone + lp * 0.3f) * vol * 0.4f;
            }

            return Normalize(buf, 0.5f);
        }

        /// <summary>
        /// Thunder crack: bright transient with long rumbling tail.
        /// Signals danger, covers scene transitions.
        /// </summary>
        private float[] SynthThunder()
        {
            const float seconds = 2.5f;
            int n = (int)(SampleRate * seconds);
            var buf = new float[n];
            var rng = new System.Random(55);

            float lp1 = 0f, lp2 = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

                // Bright crack at start
                float crack = noise * Mathf.Exp(-t * 40f) * 0.8f;

                // Long rumble tail
                lp1 += (noise - lp1) * 0.008f;
                lp2 += (lp1 - lp2) * 0.008f;
                float rumble = lp2 * Mathf.Exp(-t * 1.5f) * 3f;

                // Secondary crack (lightning has multiple strokes)
                float crack2 = 0f;
                if (t > 0.3f && t < 0.5f)
                    crack2 = noise * Mathf.Exp(-(t - 0.3f) * 30f) * 0.4f;

                buf[i] = Mathf.Clamp(crack + rumble + crack2, -1f, 1f);
            }

            return Normalize(buf, 0.9f);
        }

        private static float[] Normalize(float[] buf, float peak)
        {
            float max = 0f;
            foreach (float s in buf) max = Mathf.Max(max, Mathf.Abs(s));
            if (max < 1e-6f) return buf;

            float scale = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= scale;
            return buf;
        }

        // ───────────────────── UI construction ─────────────────────

        private void BuildUI()
        {
            // Canvas
            var canvasGO = new GameObject("SplashCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.GetComponent<CanvasScaler>().referenceResolution =
                new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            // Dark background
            var bg = CreateFullScreenImage(canvasGO.transform, "Background",
                new Color(0.02f, 0.02f, 0.02f));

            // Subtle dark vignette (not red, just darkened edges)
            vignetteImage = CreateFullScreenImage(canvasGO.transform, "Vignette",
                new Color(0f, 0f, 0f, 0.1f));

            // Subtle flash overlay (not blood red, dark orange flash)
            bloodStreakImage = CreateFullScreenImage(canvasGO.transform, "BloodStreak",
                new Color(0.3f, 0.1f, 0f, 0f));

            // Transition overlay
            bgImage = CreateFullScreenImage(canvasGO.transform, "Transition",
                new Color(0, 0, 0, 0));

            // ── Warning Text Group (first thing shown) ──
            var warningGO = new GameObject("WarningGroup", typeof(RectTransform));
            warningGO.transform.SetParent(canvasGO.transform, false);
            StretchFull(warningGO.GetComponent<RectTransform>());
            warningGroup = warningGO.AddComponent<CanvasGroup>();

            CreateText(warningGO.transform, "Warning",
                "THE ISLAND REMEMBERS...", 36,
                new Color(0.7f, 0.15f, 0.1f),  // dark blood red
                new Vector2(0, 0),
                FontStyles.Italic);

            // ── Studio Logo Group ──
            var logoGO = new GameObject("LogoGroup", typeof(RectTransform));
            logoGO.transform.SetParent(canvasGO.transform, false);
            StretchFull(logoGO.GetComponent<RectTransform>());
            logoGroup = logoGO.AddComponent<CanvasGroup>();

            // Studio name - big and bold
            CreateText(logoGO.transform, "StudioName",
                "YAARI", 140,
                new Color(0.95f, 0.55f, 0.05f),  // bright orange-gold
                new Vector2(0, 50),
                FontStyles.Bold);

            CreateText(logoGO.transform, "StudioTag",
                "G A M E S", 36,
                new Color(0.6f, 0.6f, 0.6f),
                new Vector2(0, -60),
                FontStyles.Normal);

            // Line
            var lineGO = new GameObject("Line", typeof(RectTransform), typeof(Image));
            lineGO.transform.SetParent(logoGO.transform, false);
            var lineRT = lineGO.GetComponent<RectTransform>();
            lineRT.anchoredPosition = new Vector2(0, -5);
            lineRT.sizeDelta = new Vector2(300, 2);
            lineGO.GetComponent<Image>().color = new Color(0.95f, 0.55f, 0.05f, 0.6f);
            lineRT.sizeDelta = new Vector2(400, 3);

            // ── Title Group ──
            var titleGO = new GameObject("TitleGroup", typeof(RectTransform));
            titleGO.transform.SetParent(canvasGO.transform, false);
            StretchFull(titleGO.GetComponent<RectTransform>());
            titleGroup = titleGO.AddComponent<CanvasGroup>();

            // Game title - big, aggressive
            titleTMP = CreateText(titleGO.transform, "GameTitle",
                "JUNGLE\nWARFARE", 130,
                new Color(0.95f, 0.9f, 0.85f),  // off-white
                new Vector2(0, 80),
                FontStyles.Bold);
            // Red shadow for aggression
            var outline = titleTMP.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.7f, 0.1f, 0f, 0.9f);
            outline.effectDistance = new Vector2(4, -4);
            // Second outline for depth
            var outline2 = titleTMP.gameObject.AddComponent<Outline>();
            outline2.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline2.effectDistance = new Vector2(6, -6);

            // Subtitle - war quote style
            CreateText(titleGO.transform, "Subtitle",
                "N O   O N E   G E T S   O U T   A L I V E", 22,
                new Color(0.7f, 0.15f, 0.1f),
                new Vector2(0, -55),
                FontStyles.Italic);

            // Warning label (like game ratings)
            CreateText(titleGO.transform, "Rating",
                "RATED FOR INTENSE COMBAT", 16,
                new Color(0.5f, 0.5f, 0.5f),
                new Vector2(0, -290),
                FontStyles.Normal);

            // ── Tap to Play Group ──
            var tapGO = new GameObject("TapGroup", typeof(RectTransform));
            tapGO.transform.SetParent(canvasGO.transform, false);
            StretchFull(tapGO.GetComponent<RectTransform>());
            tapGroup = tapGO.AddComponent<CanvasGroup>();

            CreateText(tapGO.transform, "TapText",
                ">>> TAP TO DEPLOY <<<", 34,
                new Color(0.95f, 0.7f, 0.2f),  // military gold
                new Vector2(0, -200),
                FontStyles.Bold);

            CreateText(tapGO.transform, "Credits",
                "© 2026 YAARI GAMES  |  ALL RIGHTS RESERVED", 14,
                new Color(0.3f, 0.3f, 0.3f),
                new Vector2(0, -330),
                FontStyles.Normal);
        }

        private static Image CreateFullScreenImage(Transform parent, string name,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            StretchFull(go.GetComponent<RectTransform>());
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static TMP_Text CreateText(Transform parent, string name,
            string text, float size, Color color, Vector2 pos, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1200, 300);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;

            return tmp;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
