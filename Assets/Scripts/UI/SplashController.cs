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

        [Header("Audio")]
        [SerializeField] private AudioSource droneSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip gunshotClip;
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

            // Darkness and the drone. Let the unease settle before anything happens.
            yield return WaitOrSkip(0.9f);

            // Something out there.
            PlayOneShot(roarClip, 0.35f, 0.9f);
            yield return WaitOrSkip(0.9f);

            // Three shots out of the dark. Impacts, not decoration: each one lands with
            // a flash and a rattle.
            for (int i = 0; i < bulletHoles.Length; i++)
            {
                if (_skip) break;
                Bang(i);
                yield return WaitOrSkip(0.42f);
            }

            yield return WaitOrSkip(0.35f);

            // The claws answer the gunfire.
            if (!_skip) yield return SlamEmblem();

            yield return WaitOrSkip(0.4f);

            // The name types itself out of the dark.
            if (!_skip) yield return RevealTitle();

            if (!_skip)
            {
                yield return FadeSubtitle(0.35f);
                yield return WaitOrSkip(0.3f);
                yield return RevealQuote();
            }

            ApplyFinalState();
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

            _nextLightningAt = Time.time + 2f;
        }

        // --------------------------------------------------------------------- beats

        private void Bang(int index)
        {
            RectTransform hole = bulletHoles[index];
            if (hole != null) hole.gameObject.SetActive(true);

            PlayOneShot(gunshotClip, 0.7f, Random.Range(0.92f, 1.05f));
            Flash(0.5f, 0.09f);
            _shakeAmplitude = Mathf.Max(_shakeAmplitude, 16f);
        }

        private IEnumerator SlamEmblem()
        {
            PlayOneShot(roarClip, 0.8f, 0.8f);
            Flash(0.65f, 0.14f);
            _shakeAmplitude = 26f;

            const float duration = 0.3f;
            float t = 0f;
            while (t < duration && !_skip)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);

                // Ease-out-back: overshoots past 1 and settles — an impact, not an arrival.
                const float k = 1.9f;
                float eased = 1f + (k + 1f) * Mathf.Pow(p - 1f, 3f) + k * Mathf.Pow(p - 1f, 2f);

                if (emblem != null)
                    emblem.localScale = Vector3.one * Mathf.LerpUnclamped(2.6f, 1f, eased);
                yield return null;
            }

            if (emblem != null) emblem.localScale = Vector3.one;
            _slammed = true;
            _creepStartedAt = Time.time;
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
                    PlayOneShot(tickClip, 0.5f, Random.Range(0.85f, 1.2f));
                    _shakeAmplitude = Mathf.Max(_shakeAmplitude, 4f);
                }

                yield return WaitOrSkip(0.085f);
            }

            _nextFlickerAt = Time.time + 1.5f;
            _nextLightningAt = Time.time + 3f;
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
            float startVolume = droneSource != null ? droneSource.volume : 0f;

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float p = t / seconds;

                if (content != null) content.alpha = 1f - p;
                if (droneSource != null) droneSource.volume = startVolume * (1f - p);
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
                _shakeAmplitude = Mathf.Lerp(_shakeAmplitude, 0f, 9f * Time.deltaTime);
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
                // A burst, not a single dip — real faulty lights stutter.
                _flickerUntil = Time.time + Random.Range(0.06f, 0.22f);
                _nextFlickerAt = Time.time + Random.Range(1.2f, 4.5f);
            }

            titleGroup.alpha = Time.time < _flickerUntil
                ? Random.Range(0.2f, 0.65f)
                : 1f;
        }

        private void UpdateCreep()
        {
            if (emblem == null || !_slammed) return;

            // Toward the viewer, too slowly to notice consciously.
            float t = Mathf.Clamp01((Time.time - _creepStartedAt) / 16f);
            emblem.localScale = Vector3.one * Mathf.Lerp(1f, 1.07f, t);
        }

        private void UpdateLightning()
        {
            if (_nextLightningAt <= 0f || Time.time < _nextLightningAt) return;

            // Double blink, like the sky snapping a photograph.
            StartCoroutine(LightningBlink());
            _nextLightningAt = Time.time + Random.Range(3.5f, 7f);
        }

        private IEnumerator LightningBlink()
        {
            Flash(0.22f, 0.05f);
            yield return new WaitForSeconds(0.1f);
            Flash(0.14f, 0.05f);
        }

        // ------------------------------------------------------------------- helpers

        private void Flash(float alpha, float seconds)
        {
            if (flashOverlay != null) StartCoroutine(FlashRoutine(alpha, seconds));
        }

        private IEnumerator FlashRoutine(float alpha, float seconds)
        {
            var hot = new Color(0.9f, 0.15f, 0.1f);

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
