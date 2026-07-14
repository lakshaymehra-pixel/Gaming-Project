using System.Collections;
using System.Collections.Generic;
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

        [Tooltip("Night jungle — birds and insects, on separate looping sources so they drift " +
                 "against each other. It is the island you are about to land on, and it goes " +
                 "quiet when the creature arrives.")]
        [SerializeField] private AudioSource[] jungleSources;

        [SerializeField] private AudioClip gunshotClip;
        [SerializeField] private AudioClip gunBurstClip;
        [SerializeField] private AudioClip explosionClip;
        [SerializeField] private AudioClip roarClip;
        [SerializeField] private AudioClip tickClip;

        [Header("Jungle mix")]
        [Tooltip("How loud the jungle sits before anything happens.")]
        [SerializeField] private float jungleVolume = 0.5f;

        [Tooltip("What it drops to once the first roar lands. Real forest goes silent when " +
                 "something big is close — that silence is what tells you it is.")]
        [SerializeField] private float jungleHushed = 0.06f;

        [Header("Backdrop")]
        [Tooltip("The camera walking into the jungle behind the UI. Optional — without it the " +
                 "intro still runs, just against a flat background.")]
        [SerializeField] private SplashCamera backdropCamera;

        [Tooltip("The soldier ahead in the trees. He stops and turns when the creature roars.")]
        [SerializeField] private SplashSoldier backdropSoldier;

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

        /// <summary>Each jungle source's volume as the builder authored it. Ducking scales
        /// these rather than overwriting them, so birds stay louder than insects the whole
        /// way down and the way back out.</summary>
        private float[] _jungleMix;

        /// <summary>The jungle's current overall level, 0..1, on top of the mix above.</summary>
        private float _jungleLevel = 1f;

        /// <summary>Every one-shot voice spawned so far, so the final fade can duck the ones
        /// still ringing. Entries go null as Unity destroys them; the fade skips those.</summary>
        private readonly List<AudioSource> _voices = new();
        private Transform _voiceRoot;

        private void Start()
        {
            // Remember the authored balance before anything touches the volumes.
            if (jungleSources != null)
            {
                _jungleMix = new float[jungleSources.Length];
                for (int i = 0; i < jungleSources.Length; i++)
                {
                    _jungleMix[i] = jungleSources[i] != null ? jungleSources[i].volume : 0f;
                }
            }

            _jungleLevel = jungleVolume;
            SetJungle(_jungleLevel);

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

            // ---- 0.0s  The cards are off. The walk starts here, not on Awake: the camera
            // covers its whole route in the time those three cards take, and a walk spent
            // behind them arrives at the roar already finished.
            if (backdropCamera != null) backdropCamera.Begin();
            if (backdropSoldier != null) backdropSoldier.Begin();

            // Darkness — but not silence. The island is already there: night jungle, birds and
            // insects, the ordinary sound of a place with nothing wrong with it. Everything
            // after this is that sound being taken away.
            yield return WaitOrSkip(0.7f);

            // ---- 0.7s  Something out there, still far off. Low and slow.
            PlayOneShot(roarClip, 0.3f, 0.75f);

            // The forest hears it before you understand it. Birds and insects cut out — the
            // hush is the first real sign that something is wrong, and it lands a beat before
            // anyone on screen reacts.
            StartCoroutine(DuckJungle(jungleHushed, 0.7f));

            yield return WaitOrSkip(0.85f);

            // ---- 1.55s  It answers itself, closer. Louder. Angrier.
            PlayOneShot(roarClip, 0.7f, 0.65f);
            _shakeAmplitude = 12f;

            // The walk stops. Not a cut — a man slowing because he has understood something,
            // and the stillness that follows is worse than the movement was.
            if (backdropCamera != null) backdropCamera.Halt(0.5f);

            // The soldier ahead hears it too. He turns back toward it, which is toward you.
            if (backdropSoldier != null && backdropCamera != null)
                backdropSoldier.Alert(backdropCamera.transform);

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

            // And the jungle stays hushed: skipping means the creature arrived, whether or not
            // you watched it. Birds do not come back for that.
            _jungleLevel = jungleHushed;
            SetJungle(_jungleLevel);
        }

        // --------------------------------------------------------------------- beats

        /// <summary>
        /// The panic fire. Two weapons' worth of sound, and each one plays where it belongs.
        ///
        /// The first shots are spaced, so they are single reports — one SFX_GunShot per
        /// bullet hole, one hole per bang, aimed and countable. The last shots are 0.1s
        /// apart, which is not aimed fire, it is a trigger held down: those become one
        /// SFX_GunBurst, played once, with their holes punched in under it.
        ///
        /// This used to run the burst clip and the single shots simultaneously for the whole
        /// volley — two recordings of different guns firing at different rates, on top of
        /// each other. That is the thing that sounded wrong.
        /// </summary>
        private IEnumerator Fusillade()
        {
            int total = bulletHoles.Length;
            if (total == 0) yield break;

            // Aimed shots, one clip each. Clamped to at least one so a short array still opens
            // with a single report rather than starting on a burst — but never to more than
            // there are holes, or the volley fires at an index that does not exist.
            int singles = Mathf.Clamp(total - BurstShots, 1, total);

            for (int i = 0; i < singles; i++)
            {
                if (_skip) yield break;

                float p = singles > 1 ? i / (float)(singles - 1) : 0f;
                Bang(i, gunshotClip, 0.85f + p * 0.25f, 22f + p * 6f);

                // Gaps close as he loses his nerve — 0.34s down to 0.16s.
                yield return WaitOrSkip(Mathf.Lerp(0.34f, 0.16f, p));
            }

            if (_skip) yield break;

            // The trigger goes down. One burst clip, and the remaining holes appear inside it
            // at the rate the recording actually fires — not on their own clock.
            yield return BurstFire(singles, total);

            // Something out there goes up.
            PlayOneShot(explosionClip, 0.6f, 0.85f);
            _shakeAmplitude = Mathf.Max(_shakeAmplitude, 25f);
            Flash(0.5f, 0.12f);
        }

        /// <summary>How many of the impacts belong to the held-trigger burst at the end.</summary>
        private const int BurstShots = 2;

        private IEnumerator BurstFire(int from, int to)
        {
            PlayOneShot(gunBurstClip, 0.95f, 1f);

            for (int i = from; i < to; i++)
            {
                Bang(i, null, 0f, 26f);              // the burst clip is the sound; no extra shot
                yield return WaitOrSkip(0.09f);
                if (_skip) yield break;
            }
        }

        /// <summary>
        /// One impact: the hole appears, the frame flashes and kicks. The clip is optional —
        /// during the burst the sound is already playing and a second one would just muddy it.
        /// </summary>
        private void Bang(int index, AudioClip clip, float volume, float shake)
        {
            if (index < 0 || index >= bulletHoles.Length) return;

            RectTransform hole = bulletHoles[index];
            if (hole != null) hole.gameObject.SetActive(true);

            if (clip != null) PlayOneShot(clip, volume, Random.Range(0.94f, 1.06f));

            // The man in the trees is the one firing. Driving his animation from here rather
            // than on his own timer is what keeps the muzzle and the report the same event.
            if (backdropSoldier != null) backdropSoldier.Fire();

            Flash(0.6f, 0.075f);
            _shakeAmplitude = Mathf.Max(_shakeAmplitude, shake);
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

            // The world flinches too. The UI shake is a 2D rattle of the canvas; without this
            // the claws land on a picture of a jungle rather than in one.
            if (backdropCamera != null) backdropCamera.Recoil(0.9f);

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
            float startJungle = _jungleLevel;

            // Snapshot the one-shots that are still sounding, so they can be ramped down from
            // wherever they happen to be rather than from some assumed level. The final roar
            // is pitched to 0.45 and so runs about twice the length of its file — it is very
            // often one of these.
            var voices = new List<AudioSource>(_voices.Count);
            var voiceVolumes = new List<float>(_voices.Count);

            foreach (AudioSource voice in _voices)
            {
                if (voice == null) continue;
                voices.Add(voice);
                voiceVolumes.Add(voice.volume);
            }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float p = t / seconds;

                if (content != null) content.alpha = 1f - p;

                // Everything, or something keeps sounding under the loading screen after the
                // picture is gone. The scored drone has usually finished by now, which is
                // exactly why fading it alone was never enough.
                if (droneSource != null) droneSource.volume = droneVolume * (1f - p);
                if (bedSource != null) bedSource.volume = bedVolume * (1f - p);

                _jungleLevel = startJungle * (1f - p);
                SetJungle(_jungleLevel);

                for (int i = 0; i < voices.Count; i++)
                {
                    if (voices[i] != null) voices[i].volume = voiceVolumes[i] * (1f - p);
                }

                yield return null;
            }

            if (_load != null) _load.allowSceneActivation = true;
        }

        /// <summary>
        /// Sets the jungle's overall level, keeping the authored balance between its sources —
        /// birds over insects, whatever the builder decided — because ducking a mix by
        /// overwriting each volume with the same number is how a mix stops being a mix.
        /// </summary>
        private void SetJungle(float level)
        {
            if (jungleSources == null || _jungleMix == null) return;

            for (int i = 0; i < jungleSources.Length; i++)
            {
                if (jungleSources[i] != null)
                    jungleSources[i].volume = _jungleMix[i] * level;
            }
        }

        private IEnumerator DuckJungle(float to, float seconds)
        {
            float from = _jungleLevel;
            float t = 0f;

            while (t < seconds)
            {
                // Bail on a skip: ApplyFinalState has already snapped the jungle to where the
                // end of the sequence leaves it, and this fade would spend the next half
                // second dragging it back to a level the picture has moved past.
                if (_skip) yield break;

                t += Time.deltaTime;
                _jungleLevel = Mathf.Lerp(from, to, t / seconds);
                SetJungle(_jungleLevel);
                yield return null;
            }

            _jungleLevel = to;
            SetJungle(_jungleLevel);
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

        /// <summary>
        /// Plays a clip at its own pitch, on its own voice.
        ///
        /// PlayOneShot cannot do this: pitch lives on the AudioSource, not on the shot, so a
        /// gunshot pitched to 1.05 was also re-pitching the roar still ringing out underneath
        /// it. Every overlapping sound in this intro was dragging the others around. So each
        /// shot gets a throwaway AudioSource, and nothing it does is audible on anything else.
        ///
        /// They are kept in _voices so the final fade can find them. A roar started at pitch
        /// 0.45 runs twice the length of its file, so the last one is very often still
        /// sounding when the player taps — and a fade that ducks the drone and the jungle but
        /// not the roar leaves it blaring over a black screen until the scene unload cuts it.
        /// </summary>
        private void PlayOneShot(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;

            if (_voiceRoot == null)
            {
                _voiceRoot = new GameObject("Voices").transform;
                _voiceRoot.SetParent(transform, false);
            }

            var go = new GameObject($"SFX_{clip.name}");
            go.transform.SetParent(_voiceRoot, false);

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = 0f;
            source.Play();

            _voices.Add(source);

            // Pitch stretches the clip: a shot at 0.5 runs twice as long as the file.
            Destroy(go, clip.length / Mathf.Max(0.01f, pitch) + 0.1f);
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
            {
                _load = SceneManager.LoadSceneAsync(nextSceneName);
            }
            else if (SceneManager.sceneCountInBuildSettings > 1)
            {
                // Whatever scene is next in the build settings. Falling through to the game
                // beats stranding the player on a splash that loads nothing, and this is the
                // path taken when the Login scene has not been built yet.
                Debug.LogWarning($"Splash: '{nextSceneName}' is not in the build settings — " +
                                 "falling back to the next scene. Run Game > Build Login Scene.");
                _load = SceneManager.LoadSceneAsync(1);
            }
            else
            {
                Debug.LogWarning("Splash: no next scene in the build settings at all. " +
                                 "Run Game > Build Island Scene, then Build Login Scene.");
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
