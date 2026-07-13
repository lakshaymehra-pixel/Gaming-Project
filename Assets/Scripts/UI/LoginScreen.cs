using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// BGMI-style login/welcome screen shown after the splash.
    /// Offers guest login or username entry before entering the main game.
    /// </summary>
    public class LoginScreen : MonoBehaviour
    {
        [Header("UI Groups")]
        [SerializeField] private CanvasGroup mainGroup;
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private CanvasGroup buttonsGroup;
        [SerializeField] private CanvasGroup usernameGroup;
        [SerializeField] private CanvasGroup termsGroup;
        [SerializeField] private Image backgroundOverlay;

        [Header("Input")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_Text welcomeText;
        [SerializeField] private TMP_Text errorText;

        [Header("Buttons")]
        [SerializeField] private Button guestButton;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button googleButton;

        [Header("Next Scene")]
        [SerializeField] private string nextSceneName = "Island";

        [Header("Audio")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip clickClip;

        private bool _transitioning;

        private void Start()
        {
            // Fade in
            if (mainGroup != null) mainGroup.alpha = 0f;
            StartCoroutine(IntroSequence());

            // Wire buttons
            if (guestButton != null) guestButton.onClick.AddListener(OnGuestLogin);
            if (loginButton != null) loginButton.onClick.AddListener(OnUsernameLogin);
            if (googleButton != null) googleButton.onClick.AddListener(OnGoogleLogin);
        }

        private IEnumerator IntroSequence()
        {
            yield return new WaitForSeconds(0.3f);

            // Fade in main
            yield return FadeGroup(mainGroup, 0f, 1f, 0.8f);

            yield return new WaitForSeconds(0.2f);

            // Title fades in
            yield return FadeGroup(titleGroup, 0f, 1f, 0.6f);

            yield return new WaitForSeconds(0.3f);

            // Buttons slide in
            yield return FadeGroup(buttonsGroup, 0f, 1f, 0.5f);

            // Terms at bottom
            yield return FadeGroup(termsGroup, 0f, 1f, 0.4f);
        }

        // ────────────────────── Button Handlers ──────────────────────

        private void OnGuestLogin()
        {
            if (_transitioning) return;
            PlayClick();

            // Save as guest
            string guestName = "Soldier_" + Random.Range(1000, 9999);
            PlayerPrefs.SetString("PlayerName", guestName);
            PlayerPrefs.Save();

            if (welcomeText != null)
                welcomeText.text = $"Welcome, {guestName}!";

            StartCoroutine(TransitionToGame());
        }

        private void OnUsernameLogin()
        {
            if (_transitioning) return;
            PlayClick();

            if (usernameInput == null) return;

            string name = usernameInput.text.Trim();
            if (string.IsNullOrEmpty(name) || name.Length < 3)
            {
                if (errorText != null)
                {
                    errorText.text = "Username must be at least 3 characters!";
                    errorText.gameObject.SetActive(true);
                }
                return;
            }

            if (name.Length > 16)
            {
                if (errorText != null)
                {
                    errorText.text = "Username must be under 16 characters!";
                    errorText.gameObject.SetActive(true);
                }
                return;
            }

            // Save username
            PlayerPrefs.SetString("PlayerName", name);
            PlayerPrefs.Save();

            if (errorText != null) errorText.gameObject.SetActive(false);
            if (welcomeText != null)
                welcomeText.text = $"Welcome, {name}!";

            StartCoroutine(TransitionToGame());
        }

        private void OnGoogleLogin()
        {
            if (_transitioning) return;
            PlayClick();

            // Google sign-in placeholder — show message
            if (errorText != null)
            {
                errorText.text = "Google Sign-In coming soon!";
                errorText.color = new Color(1f, 0.8f, 0.3f);
                errorText.gameObject.SetActive(true);
            }
        }

        // ────────────────────── Transition ──────────────────────

        private IEnumerator TransitionToGame()
        {
            _transitioning = true;

            // Show welcome text
            if (usernameGroup != null)
            {
                usernameGroup.alpha = 0f;
                usernameGroup.gameObject.SetActive(true);
                yield return FadeGroup(usernameGroup, 0f, 1f, 0.5f);
            }

            yield return new WaitForSeconds(1.0f);

            // Fade out everything
            if (bgmSource != null)
                StartCoroutine(FadeAudio(bgmSource, 0.8f));

            yield return FadeGroup(mainGroup, 1f, 0f, 0.6f);

            // Load game
            SceneManager.LoadScene(nextSceneName);
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
