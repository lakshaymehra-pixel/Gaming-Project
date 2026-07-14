using System.Collections;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The lobby: who you are, where you are going, and the button that takes you there.
    ///
    /// It sits between the login and the map, which is the only place a game gets to show a
    /// player what they have done. Everything on it comes from PlayerProfile.
    /// </summary>
    public class LobbyScreen : MonoBehaviour
    {
        [Header("Player card")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text xpText;
        [SerializeField] private Image xpBar;
        [SerializeField] private Image avatar;
        [SerializeField] private TMP_Text avatarInitial;

        [Header("Stats")]
        [SerializeField] private TMP_Text killsText;
        [SerializeField] private TMP_Text matchesText;
        [SerializeField] private TMP_Text bestWaveText;

        [Header("Maps")]
        [Tooltip("The scene each card loads, in card order.")]
        [SerializeField] private string mapAScene = "Island";
        [SerializeField] private string mapBScene = "Arena";

        [SerializeField] private Button mapAButton;
        [SerializeField] private Button mapBButton;
        [SerializeField] private Image mapAOutline;
        [SerializeField] private Image mapBOutline;
        [SerializeField] private TMP_Text mapAStatus;
        [SerializeField] private TMP_Text mapBStatus;

        [Header("Play")]
        [SerializeField] private Button playButton;
        [SerializeField] private CanvasGroup loadingGroup;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private TMP_Text percentText;
        [SerializeField] private Image loadingBar;

        [Header("Settings")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private SettingsPanel settingsPanel;

        [Header("Sign out")]
        [SerializeField] private Button signOutButton;
        [SerializeField] private string loginScene = "Login";

        [Header("Level up")]
        [Tooltip("Shown once, on the first lobby after a level-up.")]
        [SerializeField] private CanvasGroup levelUpBanner;
        [SerializeField] private TMP_Text levelUpText;

        private int _selected;
        private AsyncOperation _load;
        private bool _launching;

        private void Start()
        {
            PlayerProfile.Load();

            RefreshCard();
            SetUpMaps();

            if (playButton != null) playButton.onClick.AddListener(OnPlay);
            if (settingsButton != null && settingsPanel != null)
                settingsButton.onClick.AddListener(settingsPanel.Open);
            if (signOutButton != null) signOutButton.onClick.AddListener(OnSignOut);

            if (loadingGroup != null) loadingGroup.alpha = 0f;
            if (levelUpBanner != null) levelUpBanner.alpha = 0f;

            if (PlayerProfile.PendingLevelUp) StartCoroutine(CelebrateLevelUp());
        }

        // ---------------------------------------------------------------- the player

        private void RefreshCard()
        {
            PlayerProfile.ProfileData p = PlayerProfile.Current;

            if (nameText != null) nameText.text = p.displayName.ToUpperInvariant();
            if (levelText != null) levelText.text = "LVL " + p.level;

            if (xpBar != null) xpBar.fillAmount = PlayerProfile.LevelProgress;
            if (xpText != null)
                xpText.text = $"{p.xp} / {PlayerProfile.XpForLevel(p.level)} XP";

            if (killsText != null) killsText.text = p.totalKills.ToString();
            if (matchesText != null) matchesText.text = p.matchesPlayed.ToString();
            if (bestWaveText != null) bestWaveText.text = p.bestWave.ToString();

            // No avatar art exists, and inventing a portrait system to fill a 100px square is
            // how a lobby turns into a month. An initial on a colour derived from the name is
            // honest, distinct per player, and free.
            if (avatarInitial != null)
                avatarInitial.text = p.displayName.Length > 0
                    ? p.displayName.Substring(0, 1).ToUpperInvariant()
                    : "?";

            if (avatar != null)
                avatar.color = ColorFromName(p.displayName);
        }

        /// <summary>A stable colour per name. Same player, same colour, every time.</summary>
        private static Color ColorFromName(string name)
        {
            int hash = 0;
            foreach (char c in name) hash = hash * 31 + c;

            // (x % 360 + 360) % 360 rather than Abs: Mathf.Abs(int.MinValue) returns int.MinValue,
            // and a negative hue is a black avatar for exactly one unlucky player.
            float hue = ((hash % 360) + 360) % 360 / 360f;

            // Hue from the hash, saturation and value fixed — a fully random colour is sometimes
            // mud, and this square has to sit next to gold text.
            return Color.HSVToRGB(hue, 0.55f, 0.45f);
        }

        private IEnumerator CelebrateLevelUp()
        {
            PlayerProfile.ClearLevelUpFlag();

            if (levelUpBanner == null) yield break;
            if (levelUpText != null)
                levelUpText.text = $"LEVEL {PlayerProfile.Current.level}";

            yield return Fade(levelUpBanner, 0f, 1f, 0.4f);
            yield return new WaitForSeconds(2.2f);
            yield return Fade(levelUpBanner, 1f, 0f, 0.6f);
        }

        // ------------------------------------------------------------------ the maps

        private void SetUpMaps()
        {
            if (mapAButton != null) mapAButton.onClick.AddListener(() => Select(0));
            if (mapBButton != null) mapBButton.onClick.AddListener(() => Select(1));

            // A map that was never built is not in the build settings, and loading it would drop
            // the player on a black screen. Say which menu item builds it instead.
            bool aBuilt = Application.CanStreamedLevelBeLoaded(mapAScene);
            bool bBuilt = Application.CanStreamedLevelBeLoaded(mapBScene);

            MarkUnbuilt(mapAButton, mapAStatus, aBuilt, mapAScene);
            MarkUnbuilt(mapBButton, mapBStatus, bBuilt, mapBScene);

            // With nothing to deploy to, DEPLOY has to be dead — and has to look dead. Left
            // enabled it swallowed the tap and logged to a console no player will ever see,
            // which from the outside is a button that does nothing and a game that is broken.
            if (playButton != null) playButton.interactable = aBuilt || bBuilt;

            if (!aBuilt && !bBuilt)
            {
                if (loadingText != null)
                    loadingText.text = "NO MAPS BUILT — run Game > Build Island Scene";
                if (loadingGroup != null) loadingGroup.alpha = 1f;
                return;
            }

            // Open on the map they played last, unless that is the one that does not exist.
            int remembered = PlayerProfile.Current.lastMap == mapBScene ? 1 : 0;
            bool rememberedIsBuilt = remembered == 0 ? aBuilt : bBuilt;

            Select(rememberedIsBuilt ? remembered : (aBuilt ? 0 : 1));
        }

        private static void MarkUnbuilt(Button button, TMP_Text status, bool built, string scene)
        {
            if (status != null)
                status.text = built ? "" : $"NOT BUILT — run Game > Build {scene} Scene";

            if (button != null) button.interactable = built;
        }

        private void Select(int index)
        {
            _selected = index;

            if (mapAOutline != null)
                mapAOutline.color = index == 0 ? UiGold : UiGoldOff;
            if (mapBOutline != null)
                mapBOutline.color = index == 1 ? UiGold : UiGoldOff;

            PlayerProfile.Current.lastMap = index == 0 ? mapAScene : mapBScene;
            PlayerProfile.Save();
        }

        private static readonly Color UiGold = new(0.92f, 0.72f, 0.15f, 1f);
        private static readonly Color UiGoldOff = new(0.3f, 0.28f, 0.25f, 0.5f);

        // ---------------------------------------------------------------------- play

        private void OnPlay()
        {
            if (_launching) return;
            StartCoroutine(Launch());
        }

        /// <summary>
        /// Loads the chosen map behind a bar, and holds it until it is ready. Same shape as the
        /// login's: the island takes real seconds, and a frozen lobby with no bar is how a game
        /// reads as crashed.
        /// </summary>
        private IEnumerator Launch()
        {
            _launching = true;

            string scene = _selected == 0 ? mapAScene : mapBScene;

            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogWarning($"Lobby: '{scene}' is not in the build settings. " +
                                 $"Run Game > Build {scene} Scene.");
                _launching = false;
                yield break;
            }

            if (playButton != null) playButton.interactable = false;
            if (settingsButton != null) settingsButton.interactable = false;

            if (loadingGroup != null) yield return Fade(loadingGroup, 0f, 1f, 0.3f);

            _load = SceneManager.LoadSceneAsync(scene);
            _load.allowSceneActivation = false;

            if (loadingText != null) loadingText.text = "DEPLOYING";

            // Unity parks an async load at 0.9 waiting for permission to swap, so 0.9 of its
            // progress is 100% of the work. Rescaled, or the bar sits at nine-tenths looking hung.
            while (_load.progress < 0.9f)
            {
                SetBar(_load.progress / 0.9f);
                yield return null;
            }

            SetBar(1f);
            if (loadingText != null) loadingText.text = "READY";

            yield return new WaitForSeconds(0.25f);

            _load.allowSceneActivation = true;
        }

        private void SetBar(float t)
        {
            t = Mathf.Clamp01(t);
            if (loadingBar != null) loadingBar.fillAmount = t;
            if (percentText != null) percentText.text = Mathf.RoundToInt(t * 100f) + "%";
        }

        // ------------------------------------------------------------------ sign out

        private void OnSignOut()
        {
            // The profile stays on the device under its own key — signing out is not deleting an
            // account, and coming back to a wiped level 1 would be a bug people would remember.
            PlayerPrefs.DeleteKey("PlayerName");
            PlayerPrefs.DeleteKey("LoginMethod");
            PlayerPrefs.Save();

            SceneManager.LoadScene(loginScene);
        }

        // ------------------------------------------------------------------- helpers

        private static IEnumerator Fade(CanvasGroup group, float from, float to, float seconds)
        {
            if (group == null) yield break;

            group.alpha = from;
            float t = 0f;

            while (t < seconds)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }

            group.alpha = to;
        }
    }
}
