using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Exact BGMI-style login screen:
    /// - Full dark background with floating particles
    /// - Game logo large at top
    /// - Bottom strip with social login icons (Google, Facebook, Twitter, Guest)
    /// - "Tap to continue" after login
    /// - Terms/Privacy at very bottom
    /// - Background music loop
    /// </summary>
    public class LoginScreen : MonoBehaviour
    {
        [Header("UI Groups")]
        [SerializeField] private CanvasGroup mainGroup;
        [SerializeField] private CanvasGroup logoGroup;
        [SerializeField] private CanvasGroup bottomGroup;
        [SerializeField] private CanvasGroup loadingGroup;
        [SerializeField] private CanvasGroup termsGroup;
        [SerializeField] private Image backgroundImage;

        [Header("Particles")]
        [SerializeField] private RectTransform particleContainer;

        [Header("Sign in")]
        [SerializeField] private TMP_InputField usernameField;
        [SerializeField] private TMP_InputField passwordField;
        [SerializeField] private Button signInButton;
        [SerializeField] private Button registerButton;
        [SerializeField] private Button guestButton;

        [Tooltip("Where a rejected login says why.")]
        [SerializeField] private TMP_Text errorText;

        [Tooltip("Shown when there is no auth backend compiled in, so nobody mistakes the " +
                 "local account store for a real one.")]
        [SerializeField] private TMP_Text offlineNotice;

        [Header("Loading")]
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private TMP_Text percentText;
        [SerializeField] private TMP_Text tapText;
        [SerializeField] private Image loadingBar;

        [Header("Next Scene")]
        [SerializeField] private string nextSceneName = "Island";

        [Header("Audio")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip clickClip;

        private bool _loggedIn;
        private bool _transitioning;
        private bool _introDone;
        private bool _busy;          // an auth attempt is in flight

        /// <summary>
        /// Set only once the tap prompt is actually on screen. Not the same thing as _loggedIn,
        /// which goes true on the first line of the loading sequence — three seconds and a whole
        /// loading presentation before there is anything inviting a tap. Gating the tap handler
        /// on _loggedIn meant any stray touch in that window silently skipped the lot.
        /// </summary>
        private bool _awaitingTap;

        private Image[] _particles;

        /// <summary>The island, loading in the background. Started the moment the player picks
        /// a login method, so the bar in front of them is reporting real work.</summary>
        private AsyncOperation _load;

        private void Start()
        {
            // Start hidden, intro will fade in
            if (mainGroup != null) mainGroup.alpha = 0f;
            if (logoGroup != null) logoGroup.alpha = 0f;
            if (bottomGroup != null) bottomGroup.alpha = 0f;
            if (loadingGroup != null) loadingGroup.alpha = 0f;
            if (termsGroup != null) termsGroup.alpha = 0f;

            CreateParticles();
            StartCoroutine(IntroSequence());

            if (signInButton != null) signInButton.onClick.AddListener(OnSignIn);
            if (registerButton != null) registerButton.onClick.AddListener(OnRegister);
            if (guestButton != null) guestButton.onClick.AddListener(OnGuest);

            if (errorText != null) errorText.text = "";

            // Say so rather than letting it pass for the real thing. There is no server behind
            // this build, and a login screen that quietly accepts anything is worse than one
            // that admits what it is.
            if (offlineNotice != null)
            {
                offlineNotice.gameObject.SetActive(!Game.Core.AuthService.IsLive);
                offlineNotice.text = "OFFLINE MODE — accounts are stored on this device only";
            }

            // Come back to the name they used last. Only the name; never the password.
            if (usernameField != null)
                usernameField.text = PlayerPrefs.GetString("PlayerName", "");
        }

        private void Update()
        {
            AnimateParticles();
            AnimateTitle();

            // A tap only means "enter the game" once the screen has asked for one. Before that
            // it means nothing, because the player is watching a loading bar.
            if (_awaitingTap && !_transitioning)
            {
                if (tapText != null)
                    tapText.alpha = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);

                if (Input.anyKeyDown || (Input.touchCount > 0 &&
                    Input.GetTouch(0).phase == TouchPhase.Began))
                {
                    StartCoroutine(EnterGame());
                }
            }
        }

        /// <summary>
        /// A slow breath on the title. Gated on _introDone rather than on the group's own alpha:
        /// the old test was `alpha < 0.5f`, so the moment IntroSequence faded the logo past
        /// halfway this took the value over and fought the fade for the rest of it — the logo
        /// snapped to 0.85 mid-fade and then pulsed, instead of arriving.
        /// </summary>
        private void AnimateTitle()
        {
            if (logoGroup == null || !_introDone) return;

            logoGroup.alpha = 0.88f + 0.12f * Mathf.Sin(Time.time * 1.2f);
        }

        // ────────────────────── Intro ──────────────────────

        private IEnumerator IntroSequence()
        {
            // Main group must be visible for children to show
            if (mainGroup != null) mainGroup.alpha = 1f;

            yield return new WaitForSeconds(0.3f);

            // Logo fades in
            Debug.Log("[Login] Showing logo...");
            yield return FadeGroup(logoGroup, 0f, 1f, 0.8f);

            yield return new WaitForSeconds(0.3f);

            // Bottom buttons fade in
            Debug.Log("[Login] Showing buttons...");
            yield return FadeGroup(bottomGroup, 0f, 1f, 0.5f);

            // Terms
            yield return FadeGroup(termsGroup, 0f, 1f, 0.3f);

            // Only now does the title start breathing — it has arrived, and the pulse is not
            // allowed to touch the fade that got it here.
            _introDone = true;
        }

        // ────────────────────── Login ──────────────────────

        private void OnSignIn() => Attempt(done =>
            Game.Core.AuthService.SignIn(Username, Password, done), "Password");

        private void OnRegister() => Attempt(done =>
            Game.Core.AuthService.Register(Username, Password, done), "Password");

        private void OnGuest() => Attempt(
            Game.Core.AuthService.SignInAsGuest, "Guest");

        private string Username => usernameField != null ? usernameField.text.Trim() : "";
        private string Password => passwordField != null ? passwordField.text : "";

        /// <summary>
        /// Runs an auth attempt and, only if it succeeds, hands off to the loading sequence. A
        /// rejection puts the reason on screen and leaves the player exactly where they were,
        /// with what they typed still in the fields — retyping a username because the password
        /// was wrong is the kind of thing that makes people close a game.
        /// </summary>
        private void Attempt(System.Func<System.Action<Game.Core.AuthService.Result>, IEnumerator>
                             attempt, string method)
        {
            if (_busy || _loggedIn || _transitioning) return;
            StartCoroutine(AttemptRoutine(attempt, method));
        }

        private IEnumerator AttemptRoutine(
            System.Func<System.Action<Game.Core.AuthService.Result>, IEnumerator> attempt,
            string method)
        {
            _busy = true;
            PlayClick();

            if (errorText != null) errorText.text = "";
            SetInteractable(false);

            var result = default(Game.Core.AuthService.Result);
            yield return attempt(r => result = r);

            if (!result.Success)
            {
                if (errorText != null) errorText.text = result.Error;
                SetInteractable(true);
                _busy = false;
                yield break;
            }

            PlayerPrefs.SetString("PlayerName", result.DisplayName);
            PlayerPrefs.SetString("LoginMethod", method);
            PlayerPrefs.Save();

            yield return LoginSequence(method, result.DisplayName);
            _busy = false;
        }

        /// <summary>Locks the form while an attempt is in flight, so a second tap cannot start
        /// a second one on top of the first.</summary>
        private void SetInteractable(bool on)
        {
            if (usernameField != null) usernameField.interactable = on;
            if (passwordField != null) passwordField.interactable = on;
            if (signInButton != null) signInButton.interactable = on;
            if (registerButton != null) registerButton.interactable = on;
            if (guestButton != null) guestButton.interactable = on;
        }

        private IEnumerator LoginSequence(string method, string playerName)
        {
            _loggedIn = true;

            // The island starts loading the moment they pick a method. Everything the bar shows
            // from here is real: the login steps run in front of work that is genuinely
            // happening, rather than in front of a timer.
            BeginLoad();

            yield return FadeGroup(bottomGroup, 1f, 0f, 0.3f);

            if (loadingGroup != null)
            {
                loadingGroup.alpha = 0f;
                yield return FadeGroup(loadingGroup, 0f, 1f, 0.4f);
            }

            // The account half. These are the only fake seconds in the sequence, and they are
            // honest ones — there is no server, and a login that returns instantly reads as a
            // login that did not happen. The bar covers its first third here.
            yield return Step("CONNECTING", 0f, 0.12f, 0.7f);
            yield return Step("AUTHENTICATING", 0.12f, 0.24f, 0.6f);
            yield return Step($"WELCOME, {playerName.ToUpperInvariant()}", 0.24f, 0.3f, 0.5f);

            // And the real half: the rest of the bar is the island. Unity parks an async load at
            // 0.9 and waits for permission to swap scenes, so 0.9 of its progress is 100% of the
            // work — rescaled here, or the bar would stall at nine-tenths and look hung.
            if (loadingText != null) loadingText.text = "LOADING RESOURCES";

            while (_load != null && _load.progress < 0.9f)
            {
                float p = Mathf.Lerp(0.3f, 1f, _load.progress / 0.9f);
                SetBar(p);
                yield return null;
            }

            SetBar(1f);
            if (loadingText != null) loadingText.text = "READY";

            yield return new WaitForSeconds(0.35f);

            if (tapText != null)
            {
                tapText.gameObject.SetActive(true);
                tapText.alpha = 0f;
            }

            // Now, and not before: the prompt is up, so a tap has something to mean.
            _awaitingTap = true;
        }

        private void BeginLoad()
        {
            if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
            {
                Debug.LogWarning($"Login: '{nextSceneName}' is not in the build settings. " +
                                 "Run Game > Build Island Scene.");
                return;
            }

            _load = SceneManager.LoadSceneAsync(nextSceneName);

            // Held until they tap. Without this Unity swaps scenes the instant the load
            // finishes and the bar, the welcome, and the tap prompt are never seen.
            _load.allowSceneActivation = false;
        }

        /// <summary>One labelled step of the login half, filling its slice of the bar.</summary>
        private IEnumerator Step(string label, float from, float to, float seconds)
        {
            if (loadingText != null) loadingText.text = label;

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                SetBar(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }

            SetBar(to);
        }

        private void SetBar(float fill)
        {
            if (loadingBar != null) loadingBar.fillAmount = Mathf.Clamp01(fill);
            if (percentText != null)
                percentText.text = Mathf.RoundToInt(Mathf.Clamp01(fill) * 100f) + "%";
        }

        // ────────────────────── Enter Game ──────────────────────

        private IEnumerator EnterGame()
        {
            _transitioning = true;
            PlayClick();

            if (bgmSource != null) StartCoroutine(FadeAudio(bgmSource, 0.6f));
            yield return FadeGroup(mainGroup, 1f, 0f, 0.5f);

            // The island is already in memory — this just lets it through, so the swap is
            // instant. Loading it synchronously here would have frozen the app on the last
            // frame of the fade, which is exactly where a stall is most obvious.
            if (_load != null)
            {
                _load.allowSceneActivation = true;
                yield break;
            }

            // No async load ever started (the scene is missing from the build settings, and
            // BeginLoad said so). Try anyway rather than stranding the player on a dead screen.
            SceneManager.LoadScene(nextSceneName);
        }

        // ────────────────────── Particles ──────────────────────

        private void CreateParticles()
        {
            if (particleContainer == null) return;

            _particles = new Image[30];
            for (int i = 0; i < _particles.Length; i++)
            {
                var go = new GameObject($"Particle_{i}", typeof(RectTransform),
                    typeof(Image));
                go.transform.SetParent(particleContainer, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(
                    Random.Range(0f, 1f), Random.Range(0f, 1f));
                float size = Random.Range(2f, 6f);
                rt.sizeDelta = new Vector2(size, size);

                var img = go.GetComponent<Image>();
                float brightness = Random.Range(0.15f, 0.4f);
                img.color = new Color(brightness, brightness * 0.8f, brightness * 0.5f,
                    Random.Range(0.1f, 0.4f));
                img.raycastTarget = false;

                _particles[i] = img;
            }
        }

        private void AnimateParticles()
        {
            if (_particles == null) return;

            for (int i = 0; i < _particles.Length; i++)
            {
                if (_particles[i] == null) continue;
                var rt = _particles[i].rectTransform;

                // Slow upward drift
                float speed = 5f + i * 1.5f;
                rt.anchoredPosition += Vector2.up * speed * Time.deltaTime;

                // Slight horizontal sway
                float sway = Mathf.Sin(Time.time * (0.3f + i * 0.05f) + i) * 0.3f;
                var pos = rt.anchoredPosition;
                pos.x += sway;
                rt.anchoredPosition = pos;

                // Wrap around when off screen
                if (rt.anchoredPosition.y > 600f)
                {
                    rt.anchoredPosition = new Vector2(
                        Random.Range(-900f, 900f), -600f);
                }

                // Pulse alpha
                var c = _particles[i].color;
                c.a = (0.1f + 0.3f * Mathf.Sin(Time.time + i)) * 0.5f;
                _particles[i].color = c;
            }
        }

        // ────────────────────── Helpers ──────────────────────

        private void PlayClick()
        {
            if (sfxSource != null && clickClip != null)
                sfxSource.PlayOneShot(clickClip, 0.7f);
        }

        private static IEnumerator FadeGroup(CanvasGroup group, float from, float to,
            float duration)
        {
            if (group == null) yield break;
            float t = 0f;
            group.alpha = from;
            while (t < duration)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            group.alpha = to;
        }

        private static IEnumerator FadeAudio(AudioSource src, float duration)
        {
            if (src == null) yield break;
            float startVol = src.volume;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                src.volume = Mathf.Lerp(startVol, 0f, t / duration);
                yield return null;
            }
            src.Stop();
        }
    }
}
