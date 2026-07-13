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

        [Header("Buttons")]
        [SerializeField] private Button googleButton;
        [SerializeField] private Button facebookButton;
        [SerializeField] private Button twitterButton;
        [SerializeField] private Button guestButton;

        [Header("Loading")]
        [SerializeField] private TMP_Text loadingText;
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
        private float _loadingProgress;
        private Image[] _particles;

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

            Debug.Log("[Login] Started - fading in...");

            if (googleButton != null) googleButton.onClick.AddListener(() => OnLogin("Google"));
            if (facebookButton != null) facebookButton.onClick.AddListener(() => OnLogin("Facebook"));
            if (twitterButton != null) twitterButton.onClick.AddListener(() => OnLogin("Twitter"));
            if (guestButton != null) guestButton.onClick.AddListener(() => OnLogin("Guest"));
        }

        private void Update()
        {
            AnimateParticles();
            AnimateTitle();

            // Tap to continue after logged in
            if (_loggedIn && !_transitioning)
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

        /// <summary>Subtle glow pulse on the title for a living feel.</summary>
        private void AnimateTitle()
        {
            if (logoGroup == null || logoGroup.alpha < 0.5f) return;

            // Gentle breathing glow
            float glow = 0.85f + 0.15f * Mathf.Sin(Time.time * 1.2f);
            logoGroup.alpha = glow;
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
            Debug.Log("[Login] Ready for input");
        }

        // ────────────────────── Login ──────────────────────

        private void OnLogin(string method)
        {
            if (_loggedIn || _transitioning) return;
            PlayClick();

            string playerName = method == "Guest"
                ? "Soldier_" + Random.Range(1000, 9999)
                : method + "_Player";

            PlayerPrefs.SetString("PlayerName", playerName);
            PlayerPrefs.SetString("LoginMethod", method);
            PlayerPrefs.Save();

            StartCoroutine(LoginSequence(method, playerName));
        }

        private IEnumerator LoginSequence(string method, string playerName)
        {
            _loggedIn = true;

            // Hide buttons, show loading
            yield return FadeGroup(bottomGroup, 1f, 0f, 0.3f);

            if (loadingGroup != null)
            {
                loadingGroup.alpha = 0f;
                yield return FadeGroup(loadingGroup, 0f, 1f, 0.4f);
            }

            // Fake loading animation
            if (loadingText != null)
                loadingText.text = "Connecting...";

            yield return AnimateLoading("Connecting...", 0f, 0.3f, 1.0f);
            yield return AnimateLoading("Authenticating...", 0.3f, 0.6f, 0.8f);
            yield return AnimateLoading("Loading profile...", 0.6f, 0.9f, 0.6f);
            yield return AnimateLoading($"Welcome, {playerName}", 0.9f, 1f, 0.4f);

            yield return new WaitForSeconds(0.5f);

            // Show "tap to continue"
            if (tapText != null)
            {
                tapText.gameObject.SetActive(true);
                tapText.alpha = 0f;
            }
        }

        private IEnumerator AnimateLoading(string text, float fromBar, float toBar,
            float duration)
        {
            if (loadingText != null) loadingText.text = text;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = t / duration;
                if (loadingBar != null)
                    loadingBar.fillAmount = Mathf.Lerp(fromBar, toBar, p);
                yield return null;
            }
            if (loadingBar != null) loadingBar.fillAmount = toBar;
        }

        // ────────────────────── Enter Game ──────────────────────

        private IEnumerator EnterGame()
        {
            _transitioning = true;
            PlayClick();

            if (bgmSource != null) StartCoroutine(FadeAudio(bgmSource, 0.6f));
            yield return FadeGroup(mainGroup, 1f, 0f, 0.5f);

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
