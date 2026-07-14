using System.Collections;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The settings modal. Its own component rather than more fields on LobbyScreen, because the
    /// pause menu is going to want exactly this and a lobby that owns the settings is a lobby the
    /// pause menu has to import.
    ///
    /// Every control here changes something real. A slider that does nothing is worse than no
    /// slider — the player believes it, blames the game when it does not help, and stops trusting
    /// the rest of the menu.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Button closeButton;

        [Header("Audio")]
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TMP_Text volumeValue;

        [Header("Controls")]
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private TMP_Text sensitivityValue;

        [Header("Graphics")]
        [SerializeField] private Button smoothButton;
        [SerializeField] private Button balancedButton;
        [SerializeField] private Button hdButton;
        [SerializeField] private Image smoothOutline;
        [SerializeField] private Image balancedOutline;
        [SerializeField] private Image hdOutline;

        [Header("Foliage")]
        [SerializeField] private Slider foliageSlider;
        [SerializeField] private TMP_Text foliageValue;

        private static readonly Color On = new(0.92f, 0.72f, 0.15f, 1f);
        private static readonly Color Off = new(0.3f, 0.28f, 0.25f, 0.5f);

        private void Start()
        {
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            if (closeButton != null) closeButton.onClick.AddListener(Close);

            if (volumeSlider != null)
            {
                volumeSlider.value = GameSettings.Volume;
                volumeSlider.onValueChanged.AddListener(OnVolume);
            }

            if (sensitivitySlider != null)
            {
                sensitivitySlider.value = GameSettings.Sensitivity;
                sensitivitySlider.onValueChanged.AddListener(OnSensitivity);
            }

            if (foliageSlider != null)
            {
                foliageSlider.value = GameSettings.Foliage;
                foliageSlider.onValueChanged.AddListener(OnFoliage);
            }

            if (smoothButton != null)
                smoothButton.onClick.AddListener(() => OnTier(GameSettings.Tier.Smooth));
            if (balancedButton != null)
                balancedButton.onClick.AddListener(() => OnTier(GameSettings.Tier.Balanced));
            if (hdButton != null)
                hdButton.onClick.AddListener(() => OnTier(GameSettings.Tier.HD));

            RefreshLabels();
            RefreshTier();
        }

        public void Open()
        {
            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(FadeTo(1f, true));
        }

        public void Close()
        {
            GameSettings.Save();
            StopAllCoroutines();
            StartCoroutine(FadeTo(0f, false));
        }

        // ------------------------------------------------------------------ handlers

        private void OnVolume(float v)
        {
            GameSettings.Volume = v;

            // Applied on the spot, not on close: a volume slider you cannot hear while dragging
            // is a volume slider you cannot set.
            AudioListener.volume = v;
            RefreshLabels();
        }

        private void OnSensitivity(float v)
        {
            GameSettings.Sensitivity = v;
            RefreshLabels();
        }

        private void OnFoliage(float v)
        {
            GameSettings.Foliage = v;
            RefreshLabels();
        }

        private void OnTier(GameSettings.Tier tier)
        {
            GameSettings.Graphics = tier;
            GameSettings.Apply();     // shadows and AA change immediately, in the lobby, visibly
            RefreshTier();
        }

        private void RefreshLabels()
        {
            if (volumeValue != null)
                volumeValue.text = Mathf.RoundToInt(GameSettings.Volume * 100f) + "%";

            if (sensitivityValue != null)
                sensitivityValue.text = GameSettings.SensitivityScale.ToString("0.0") + "x";

            if (foliageValue != null)
                foliageValue.text = Mathf.RoundToInt(GameSettings.Foliage * 100f) + "%";
        }

        private void RefreshTier()
        {
            GameSettings.Tier t = GameSettings.Graphics;

            if (smoothOutline != null)
                smoothOutline.color = t == GameSettings.Tier.Smooth ? On : Off;
            if (balancedOutline != null)
                balancedOutline.color = t == GameSettings.Tier.Balanced ? On : Off;
            if (hdOutline != null)
                hdOutline.color = t == GameSettings.Tier.HD ? On : Off;
        }

        private IEnumerator FadeTo(float target, bool blocks)
        {
            if (group == null) yield break;

            group.blocksRaycasts = blocks;
            group.interactable = blocks;

            float from = group.alpha;
            float t = 0f;
            const float seconds = 0.22f;

            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;   // works from a paused game too
                group.alpha = Mathf.Lerp(from, target, t / seconds);
                yield return null;
            }

            group.alpha = target;
        }
    }
}
