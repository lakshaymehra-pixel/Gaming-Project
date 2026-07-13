using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The KAAL RAAT intro: a timed sequence — gunshots punching holes in the dark, the
    /// claw emblem slamming in, the title typing itself out — while the island loads
    /// behind it. Action first, dread after; the drone and the heartbeat run under all
    /// of it.
    ///
    /// A tap during the sequence skips to the end state; a tap after it (once the load is
    /// ready) enters the game. The whole thing auto-advances if the phone was set down.
    /// </summary>
    public class SplashController : MonoBehaviour
    {
        [Header("Groups")]
        [SerializeField] private CanvasGroup content;       // final fade-out
        [SerializeField] private RectTransform shakeRoot;   // what gunshots rattle
        [SerializeField] private CanvasGroup titleGroup;    // what the flicker dims

        [Header("Elements")]
        [SerializeField] private RectTransform emblem;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private TMP_Text quote;
        [SerializeField] private Image flashOverlay;
        [SerializeField] private RectTransform[] bulletHoles;
        [SerializeField] private Image loadingFill;
        [SerializeField] private TMP_Text tapPrompt;

        [Header("BGMI-Style Intro")]
        [SerializeField] private CanvasGroup ageWarningGroup;
        [SerializeField] private CanvasGroup studioGroup;
        [SerializeField] private CanvasGroup poweredByGroup;

        [Header("Audio")]
        [Tooltip("The scored ten seconds — accelerating heartbeat, swell under the slam. " +
                 "Plays once, in step with the sequence below.")]
        [SerializeField] private AudioSource droneSource;

        [Tooltip("A plain loop that never stops. The scored clip runs out after ten seconds " +
                 "but the hold for the scene load has no fixed length, and a splash sitting " +
                 "in total silence reads as a crash.")]
        [SerializeField] private AudioSource bedSource;

        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip gunshotClip;
        [SerializeField] private AudioClip gunBurstClip;
        [SerializeField] private AudioClip explosionClip;
        [SerializeField] private AudioClip roarClip;
        [SerializeField] private AudioClip tickClip;

        [Header("Next scene")]
        [SerializeField] private string nextSceneName = "Island";

        private AsyncOperation _load;
        private bool _tapped;
        private bool _skip;
        private bool _sequenceDone;
        private bool _slammed;

        private float _shakeAmplitude;
        private float _creepStartedAt;
        private float _flickerUntil;
        private float _nextFlickerAt;
        private float _nextLightningAt;

        private void Start()
        {
            // Everything hidden; the sequence reveals it piece by piece.
            foreach (var hole in bulletHoles)
                if (hole != null) hole.gameObject.SetActive(false);

            if (emblem != null) emblem.localScale = Vector3.zero;
            if (title != null) title.maxVisibleCharacters = 0;
            if (subtitle != null) subtitle.alpha = 0f;
            if (quote != null) quote.maxVisibleCharacters = 0;
            if (tapPrompt != null) tapPrompt.alpha = 0f;
            if (flashOverlay != null) flashOverlay.color = Color.clear;
            if (content != null) content.alpha = 1f;

            StartCoroutine(Run());
        }

        private void Update()
        {
            bool pressed = Input.GetMouseButtonDown(0) || Input.touchCount > 0;
            if (pressed)
            {
                _tapped = true;
                if (!_sequenceDone) _skip = true;
            }

            UpdateShake();
            UpdateFlicker();
            UpdateCreep();
            UpdateLightning();
        }

        // ------------------------------------------------------------------ timeline

        private IEnumerator Run()
        {
            BeginLoad();

            // Hide the main horror content initially
            if (titleGroup != null) titleGroup.alpha = 0f;

            // ═══════════════════════════════════════════════════════════
            // BGMI PHASE 1: Age/Content Warning (like BGMI/PUBG)
            // ═══════════════════════════════════════════════════════════
            if (ageWarningGroup != null)
            {
                ageWarningGroup.alpha = 0f;
                yield return FadeGroup(ageWarningGroup, 0f, 1f, 0.8f);
                yield return WaitOrSkip(2.5f);
                yield return FadeGroup(ageWarningGroup, 1f, 0f, 0.6f);
                yield return WaitOrSkip(0.3f);
            }

            // ═══════════════════════════════════════════════════════════
            // BGMI PHASE 2: Powered By (engine logo)
            // ═══════════════════════════════════════════════════════════
            if (poweredByGroup != null)
            {
                poweredByGroup.alpha = 0f;
                yield return FadeGroup(poweredByGroup, 0f, 1f, 0.6f);
                yield return WaitOrSkip(1.5f);
                yield return FadeGroup(poweredByGroup, 1f, 0f, 0.5f);
                yield return WaitOrSkip(0.3f);
            }

            // ═══════════════════════════════════════════════════════════
            // BGMI PHASE 3: Studio Logo
            // ═══════════════════════════════════════════════════════════
            if (studioGroup != null)
            {
                studioGroup.alpha = 0f;
                yield return FadeGroup(studioGroup, 0f, 1f, 0.7f);
                yield return WaitOrSkip(2.0f);
                yield return FadeGroup(studioGroup, 1f, 0f, 0.5f);
                yield return WaitOrSkip(0.4f);
            }

            // ═══════════════════════════════════════════════════════════
            // HORROR PHASE: The original KAAL RAAT sequence begins
            // ═══════════════════════════════════════════════════════════

            // ---- 0.0s  Darkness. Only the drone and the heartbeat under it.
            yield return WaitOrSkip(0.7f);

            // ---- 0.7s  Something out there, still far off. Low and slow.
            PlayOneShot(roarClip, 0.3f, 0.75f);
            yield return WaitOrSkip(0.85f);

            // ---- 1.55s  It answers itself, closer. Louder. Angrier.
            PlayOneShot(roarClip, 0.7f, 0.65f);
            _shakeAmplitude = 12f;
            yield return WaitOrSkip(0.3f);

            // ---- A third roar, very close — the creature is HERE
            PlayOneShot(roarClip, 0.9f, 0.5f);
            _shakeAmplitude = 18f;
            Flash(0.3f, 0.1f);
            yield return WaitOrSkip(0.45f);

            // ---- 2.0s  Panic fire. Not paced shots — someone emptying a magazine at a
            // shape in the dark: the gaps close, the last pair almost overlap.
            yield return Fusillade();

            // ---- 3.6s  Then nothing. The silence after the firing is the scare; the
            // slam has to land in it, not on top of more noise.
            yield return WaitOrSkip(1.1f);

            // ---- 4.7s  The claws answer.
            if (!_skip) yield return SlamEmblem();

            yield return WaitOrSkip(0.55f);

            // ---- 5.6s  The name burns itself out of the dark.
            if (!_skip) yield return RevealTitle();

            // After title: gun burst + explosion = war zone chaos
            if (!_skip)
            {
                // Quick burst of auto fire
                PlayOneShot(gunBurstClip, 0.85f, 1.1f);
                _shakeAmplitude = Mathf.Max(_shakeAmplitude, 20f);
                yield return WaitOrSkip(0.3f);

                // Double shot
                PlayOneShot(gunshotClip, 1f, 0.95f);
                Flash(0.5f, 0.08f);
                yield return WaitOrSkip(0.15f);
                PlayOneShot(gunshotClip, 0.9f, 1.05f);
                Flash(0.3f, 0.06f);
                _shakeAmplitude = Mathf.Max(_shakeAmplitude, 18f);

                // Distant explosion
                yield return WaitOrSkip(0.2f);
                PlayOneShot(explosionClip, 0.5f, 0.75f);
                _shakeAmplitude = Mathf.Max(_shakeAmplitude, 12f);
            }

            if (!_skip)
            {
                yield return FadeSubtitle(0.4f);

                // Distant roar during subtitle — something is still out there
                PlayOneShot(roarClip, 0.4f, 0.55f);
                yield return WaitOrSkip(0.35f);
                yield return RevealQuote();
            }

            ApplyFinalState();

            // After everything: one final distant roar fading away
            PlayOneShot(roarClip, 0.25f, 0.45f);

            // ---- Let the finished frame breathe before the prompt appears.
            yield return WaitOrSkip(0.8f);

            _sequenceDone = true;

            // Hold for the island. The bar shows real progress; Unity parks async loads
            // at 0.9 until activation is allowed.
            _tapped = false;
            while (_load != null && _load.progress < 0.9f)
            {
                UpdateBar(_load.progress / 0.9f);
                yield return null;
            }
            UpdateBar(1f);

            if (tapPrompt != null) tapPrompt.alpha = 1f;

            float autoAdvanceAt = Time.time + 8f;
            while (!_tapped && Time.time < autoAdvanceAt)
            {
                // The prompt breathes — a static invitation reads as a dead screen.
                if (tapPrompt != null)
                    tapPrompt.alpha = 0.55f + 0.45f * Mathf.Sin(Time.time * 4f);
                yield return null;
            }

            yield return FadeOutAndEnter();
        }

        /// <summary>The end of the sequence, reachable by skip: everything visible.</summary>
        private void ApplyFinalState()
        {
            // Hide BGMI intro groups
            if (ageWarningGroup != null) ageWarningGroup.alpha = 0f;
            if (studioGroup != null) studioGroup.alpha = 0f;
            if (poweredByGroup != null) poweredByGroup.alpha = 0f;

            foreach (var hole in bulletHoles)
                if (hole != null) hole.gameObject.SetActive(true);

            if (emblem != null && !_slammed)
            {
                emblem.localScale = Vector3.one;
                _slammed = true;
                _creepStartedAt = Time.time;
            }

            if (title != null) title.maxVisibleCharacters = int.MaxValue;
            if (subtitle != null) subtitle.alpha = 1f;
            if (quote != null) quote.maxVisibleCharacters = int.MaxValue;

            // Both, not just the lightning: skipping past the slam skips the only other place
            // the flicker clock is armed, and a storm with no flicker is half a storm.
            _nextLightningAt = Time.time + 1f;
            _nextFlickerAt = Time.time + 1.5f;

            // A skip jumps to the end of the picture, so the audio scored to the picture has
            // to end with it — otherwise a tap at second two leaves the slam's swell rising
            // under a screen where the slam has already happened. The bed keeps playing.
            if (_skip && droneSource != null) droneSource.Stop();
        }

        // --------------------------------------------------------------------- beats

        /// <summary>
        /// The burst. Shots accelerate — panic fire in the dark. Uses real gun burst
        /// clip layered with individual shots for each bullet hole.
        /// </summary>
        private IEnumerator Fusillade()
        {
            // Play the full auto burst sound underneath everything
            PlayOneShot(gunBurstClip, 0.9f, Random.Range(0.92f, 1.05f));

            for (int i = 0; i < bulletHoles.Length; i++)
            {
                if (_skip) yield break;

                float p = bulletHoles.Length > 1
                    ? i / (float)(bulletHoles.Length - 1)
                    : 0f;

                // Later shots hit harder: the shooter is closer to losing it.
                Bang(i, 1f + p * 0.5f);

                yield return WaitOrSkip(Mathf.Lerp(0.34f, 0.1f, p * p));
            }

            // Explosion after the burst — something got hit
            PlayOneShot(explosionClip, 0.6f, 0.85f);
            _shakeAmplitude = Mathf.Max(_shakeAmplitude, 25f);
            Flash(0.5f, 0.12f);
        }

        private void Bang(int index, float force = 1f)
        {
            RectTransform hole = bulletHoles[index];
            if (hole != null) hole.gameObject.SetActive(true);

            PlayOneShot(gunshotClip, 0.85f * force, Random.Range(0.9f, 1.08f));
            Flash(0.62f * force, 0.075f);
            _shakeAmplitude = Mathf.Max(_shakeAmplitude, 22f * force);
        }

        /// <summary>
        /// The hit the whole intro is built around. It arrives from far in front of the
        /// screen and stops dead — the overshoot and the hard stop are what sell mass. The
        /// roar is pitched down under its own natural range so it reads as something much
        /// bigger than the thing that made the sound.
        /// </summary>
        private IEnumerator SlamEmblem()
        {
            PlayOneShot(roarClip, 1f, 0.5f);   // deeper pitch = bigger creature
            Flash(1f, 0.28f);                   // brighter flash
            _shakeAmplitude = 60f;              // harder shake

            const float duration = 0.34f;
            float t = 0f;
            while (t < duration && !_skip)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);

                // Ease-out-back: overshoots past 1 and settles — an impact, not an arrival.
                const float k = 2.6f;
                float eased = 1f + (k + 1f) * Mathf.Pow(p - 1f, 3f) + k * Mathf.Pow(p - 1f, 2f);

                if (emblem != null)
                    emblem.localScale = Vector3.one * Mathf.LerpUnclamped(4.2f, 1f, eased);
                yield return null;
            }

            if (emblem != null) emblem.localScale = Vector3.one;
            _slammed = true;
            _creepStartedAt = Time.time;

            // The aftershock: a second, smaller kick a beat late, the way a real impact
            // rings rather than simply stopping.
            _shakeAmplitude = Mathf.Max(_shakeAmplitude, 14f);

            // The storm starts here, not after the title — it should already be raging
            // while the name is still being spelled out.
            _nextLightningAt = Time.time + 0.9f;
            _nextFlickerAt = Time.time + 1.4f;
        }

        private IEnumerator RevealTitle()
        {
            if (title == null) yield break;

            string text = title.text;
            for (int i = 1; i <= text.Length; i++)
            {
                if (_skip) yield break;

                title.maxVisibleCharacters = i;

                if (!char.IsWhiteSpace(text[i - 1]))
                {
                    PlayOneShot(tickClip, 0.65f, Random.Range(0.8f, 1.25f));
                    _shakeAmplitude = Mathf.Max(_shakeAmplitude, 7f);
                    Flash(0.08f, 0.05f);
                }

                yield return WaitOrSkip(0.11f);
            }

            // The name lands: one last hit under the final letter.
            PlayOneShot(roarClip, 0.45f, 0.7f);
            _shakeAmplitude = Mathf.Max(_shakeAmplitude, 18f);
            Flash(0.35f, 0.16f);
        }

        private IEnumerator FadeSubtitle(float seconds)
        {
            if (subtitle == null) yield break;

            float t = 0f;
            while (t < seconds && !_skip)
            {
                t += Time.deltaTime;
                subtitle.alpha = t / seconds;
                yield return null;
            }
            subtitle.alpha = 1f;
        }

        private IEnumerator RevealQuote()
        {
            if (quote == null) yield break;

            string text = quote.text;
            for (int i = 1; i <= text.Length; i++)
            {
                if (_skip) yield break;
                quote.maxVisibleCharacters = i;
                yield return WaitOrSkip(0.028f);
            }
        }

        private IEnumerator FadeOutAndEnter()
        {
            const float seconds = 0.6f;

            float droneVolume = droneSource != null ? droneSource.volume : 0f;
            float bedVolume = bedSource != null ? bedSource.volume : 0f;

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float p = t / seconds;

                if (content != null) content.alpha = 1f - p;

                // Both, or the bed keeps humming under the loading screen after the picture
                // is gone. The scored drone has usually finished by now, which is exactly why
                // fading it alone was never enough.
                if (droneSource != null) droneSource.volume = droneVolume * (1f - p);
                if (bedSource != null) bedSource.volume = bedVolume * (1f - p);

                yield return null;
            }

            if (_load != null) _load.allowSceneActivation = true;
        }

        // ------------------------------------------------------------ ambient motion

        private void UpdateShake()
        {
            if (shakeRoot == null) return;

            if (_shakeAmplitude > 0.1f)
            {
                shakeRoot.anchoredPosition = Random.insideUnitCircle * _shakeAmplitude;

                // Slower decay than a snap-back: a heavy hit rings out over most of a
                // second. Faster and the slam feels like a UI tween.
                _shakeAmplitude = Mathf.Lerp(_shakeAmplitude, 0f, 6.5f * Time.deltaTime);
            }
            else
            {
                shakeRoot.anchoredPosition = Vector2.zero;
            }
        }

        private void UpdateFlicker()
        {
            if (titleGroup == null || _nextFlickerAt <= 0f) return;

            if (Time.time >= _nextFlickerAt)
            {
                // Aggressive flicker — faulty lights in a horror setting.
                _flickerUntil = Time.time + Random.Range(0.1f, 0.4f);
                _nextFlickerAt = Time.time + Random.Range(0.5f, 1.8f);
            }

            titleGroup.alpha = Time.time < _flickerUntil
                ? Random.Range(0.08f, 0.55f)
                : 1f;
        }

        private void UpdateCreep()
        {
            if (emblem == null || !_slammed) return;

            // Toward the viewer, too slowly to notice consciously — but over the ~5s the
            // emblem is actually on screen, not the 16s the old ramp assumed, or it never
            // travelled far enough to register at all.
            float t = Mathf.Clamp01((Time.time - _creepStartedAt) / 6f);
            emblem.localScale = Vector3.one * Mathf.Lerp(1f, 1.1f, t);
        }

        private void UpdateLightning()
        {
            if (_nextLightningAt <= 0f || Time.time < _nextLightningAt) return;

            StartCoroutine(LightningBlink());
            _nextLightningAt = Time.time + Random.Range(1.0f, 2.5f);
        }

        /// <summary>
        /// Double blink, like the sky snapping a photograph. Roughly one strike in three
        /// lands close: brighter, and it shakes — otherwise the storm stays wallpaper.
        /// </summary>
        private IEnumerator LightningBlink()
        {
            bool close = Random.value < 0.45f;   // more close strikes

            Flash(close ? 0.7f : 0.3f, 0.06f);
            if (close)
            {
                _shakeAmplitude = Mathf.Max(_shakeAmplitude, 14f);
                // Sometimes a close strike brings a distant roar
                if (Random.value < 0.3f)
                    PlayOneShot(roarClip, 0.15f, Random.Range(0.4f, 0.6f));
            }

            yield return new WaitForSeconds(Random.Range(0.07f, 0.13f));
            Flash(close ? 0.3f : 0.15f, 0.06f);
        }

        // ------------------------------------------------------------------- helpers

        private void Flash(float alpha, float seconds)
        {
            if (flashOverlay != null) StartCoroutine(FlashRoutine(alpha, seconds));
        }

        private IEnumerator FlashRoutine(float alpha, float seconds)
        {
            var hot = new Color(0.85f, 0.08f, 0.05f);   // deeper blood red flash

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(alpha, 0f, t / seconds);
                if (flashOverlay != null)
                    flashOverlay.color = new Color(hot.r, hot.g, hot.b, a);
                yield return null;
            }

            if (flashOverlay != null) flashOverlay.color = Color.clear;
        }

        private void PlayOneShot(AudioClip clip, float volume, float pitch)
        {
            if (sfxSource == null || clip == null) return;
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, volume);
        }

        private IEnumerator WaitOrSkip(float seconds)
        {
            float t = 0f;
            while (t < seconds && !_skip)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator FadeGroup(CanvasGroup group, float from, float to, float seconds)
        {
            if (group == null) yield break;
            float t = 0f;
            group.alpha = from;
            while (t < seconds && !_skip)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            group.alpha = to;
        }

        private void BeginLoad()
        {
            if (Application.CanStreamedLevelBeLoaded(nextSceneName))
                _load = SceneManager.LoadSceneAsync(nextSceneName);
            else if (SceneManager.sceneCountInBuildSettings > 1)
                _load = SceneManager.LoadSceneAsync(1);
            else
            {
                Debug.LogWarning("Splash: no next scene found in build settings. " +
                                 "Build the Island scene (Game > Build Island Scene).");
                return;
            }

            _load.allowSceneActivation = false;
        }

        private void UpdateBar(float t)
        {
            if (loadingFill != null) loadingFill.fillAmount = Mathf.Clamp01(t);
        }
    }
}
