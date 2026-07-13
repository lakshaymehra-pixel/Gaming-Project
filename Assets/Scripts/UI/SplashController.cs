using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Runs the splash: fade the branding in, start loading the game behind it, let the
    /// player tap through once the load is ready, fade out, switch. The load runs during
    /// the branding hold, so on most devices the splash costs zero extra wait — the time
    /// the logo is on screen is time the island was loading anyway.
    /// </summary>
    public class SplashController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup content;
        [SerializeField] private Image loadingFill;
        [SerializeField] private TMP_Text tapPrompt;

        [Header("Timing")]
        [SerializeField] private float fadeInSeconds = 0.9f;
        [SerializeField] private float minHoldSeconds = 1.4f;
        [SerializeField] private float fadeOutSeconds = 0.5f;

        [Header("Next scene")]
        [SerializeField] private string nextSceneName = "Island";

        private AsyncOperation _load;
        private bool _tapped;

        private void Start()
        {
            if (content != null) content.alpha = 0f;
            if (tapPrompt != null) tapPrompt.alpha = 0f;

            StartCoroutine(Run());
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
                _tapped = true;
        }

        private IEnumerator Run()
        {
            BeginLoad();

            yield return Fade(0f, 1f, fadeInSeconds);
            yield return new WaitForSeconds(minHoldSeconds);

            // Hold until the island is actually ready. The bar tracks real progress —
            // Unity parks async loads at 0.9 until activation is allowed.
            _tapped = false;
            while (_load != null && _load.progress < 0.9f)
            {
                UpdateBar(_load.progress / 0.9f);
                yield return null;
            }
            UpdateBar(1f);

            // Ready: invite the tap, but do not require it forever — auto-advance keeps
            // the splash from being a wall if the player put the phone down.
            if (tapPrompt != null) tapPrompt.alpha = 1f;

            float autoAdvanceAt = Time.time + 4f;
            while (!_tapped && Time.time < autoAdvanceAt)
                yield return null;

            yield return Fade(1f, 0f, fadeOutSeconds);

            if (_load != null)
                _load.allowSceneActivation = true;
        }

        private void BeginLoad()
        {
            // Prefer the named scene; fall back to whatever is next in build settings so a
            // repo where only the Arena was built still boots into something.
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

        private IEnumerator Fade(float from, float to, float seconds)
        {
            if (content == null) yield break;

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                content.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }

            content.alpha = to;
        }
    }
}
