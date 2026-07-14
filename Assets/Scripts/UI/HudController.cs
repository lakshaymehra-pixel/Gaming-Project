using Game.Core;
using Game.Player;
using Game.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Drives every readout on screen. Everything here is event-driven except the
    /// crosshair, which tracks the gun's live spread and so has to poll.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameLoop game;
        [SerializeField] private PlayerController player;

        [Header("Health")]
        [SerializeField] private Image healthFill;
        [SerializeField] private TMP_Text healthLabel;
        [SerializeField] private CanvasGroup damageVignette;
        [SerializeField] private float vignetteFadeSpeed = 2f;

        [Header("Ammo")]
        [SerializeField] private TMP_Text ammoLabel;
        [SerializeField] private GameObject reloadPrompt;

        [Header("Match")]
        [SerializeField] private TMP_Text killsLabel;
        [SerializeField] private TMP_Text waveLabel;
        [SerializeField] private TMP_Text scoreLabel;

        [Header("Crosshair")]
        [SerializeField] private RectTransform crosshair;
        [SerializeField] private float crosshairPixelsPerDegree = 26f;
        [SerializeField] private float crosshairMinSize = 24f;

        [Header("Hitmarker")]
        [SerializeField] private CanvasGroup hitmarker;
        [SerializeField] private float hitmarkerFadeSpeed = 6f;

        [Header("Screens")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TMP_Text gameOverStats;
        [SerializeField] private UnityEngine.UI.Button retryButton;
        [SerializeField] private UnityEngine.UI.Button lobbyButton;

        [Tooltip("Joystick, fire, aim, the lot. Hidden on death — they are full-screen raycast " +
                 "targets, and a dead player pawing at a FIRE button that does nothing while " +
                 "the LOBBY button sits under it is a bad way to end a match.")]
        [SerializeField] private GameObject touchControls;

        private Weapon _weapon;

        private void Start()
        {
            if (game == null || player == null)
            {
                Debug.LogError($"{name}: game or player not assigned.", this);
                enabled = false;
                return;
            }

            _weapon = player.Weapon;

            player.Health.Changed += OnHealthChanged;
            game.ScoreChanged += OnScoreChanged;
            game.PlayerDied += OnPlayerDied;
            game.GameOver += OnGameOver;
            game.Spawner.WaveStarted += OnWaveStarted;

            if (_weapon != null)
            {
                _weapon.AmmoChanged += OnAmmoChanged;
                _weapon.Hit += OnHit;
            }

            if (retryButton != null) retryButton.onClick.AddListener(OnRestartPressed);
            if (lobbyButton != null) lobbyButton.onClick.AddListener(OnLobbyPressed);

            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (hitmarker != null) hitmarker.alpha = 0f;
            if (damageVignette != null) damageVignette.alpha = 0f;

            OnHealthChanged(player.Health.Normalized);
            OnAmmoChanged();
            OnScoreChanged();
            OnWaveStarted(game.Spawner.WaveNumber);
        }

        private void OnDestroy()
        {
            if (player != null) player.Health.Changed -= OnHealthChanged;
            if (game != null)
            {
                game.ScoreChanged -= OnScoreChanged;
                game.PlayerDied -= OnPlayerDied;
                game.GameOver -= OnGameOver;
                if (game.Spawner != null) game.Spawner.WaveStarted -= OnWaveStarted;
            }
            if (_weapon != null)
            {
                _weapon.AmmoChanged -= OnAmmoChanged;
                _weapon.Hit -= OnHit;
            }
        }

        private void Update()
        {
            UpdateCrosshair();
            FadeOut(hitmarker, hitmarkerFadeSpeed);
            FadeOut(damageVignette, vignetteFadeSpeed);

            if (reloadPrompt != null && _weapon != null)
            {
                reloadPrompt.SetActive(
                    !_weapon.IsReloading &&
                    _weapon.AmmoInMagazine == 0 &&
                    _weapon.ReserveAmmo > 0);
            }
        }

        private void UpdateCrosshair()
        {
            if (crosshair == null || _weapon == null) return;

            float spread = _weapon.CurrentSpread(player.IsAiming);
            float size = Mathf.Max(
                crosshairMinSize, crosshairMinSize + spread * crosshairPixelsPerDegree);

            crosshair.sizeDelta = Vector2.Lerp(
                crosshair.sizeDelta, new Vector2(size, size), 12f * Time.deltaTime);
        }

        private static void FadeOut(CanvasGroup group, float speed)
        {
            if (group == null || group.alpha <= 0f) return;
            group.alpha = Mathf.Max(0f, group.alpha - speed * Time.deltaTime);
        }

        private void OnHealthChanged(float normalized)
        {
            if (healthFill != null) healthFill.fillAmount = normalized;
            if (healthLabel != null)
                healthLabel.text = Mathf.CeilToInt(player.Health.Current).ToString();

            // Flash red only on damage, not on regen ticks.
            if (damageVignette != null && normalized < damageVignette.alpha)
                damageVignette.alpha = 1f - normalized;
        }

        private void OnAmmoChanged()
        {
            if (ammoLabel == null || _weapon == null) return;
            ammoLabel.text = $"{_weapon.AmmoInMagazine} / {_weapon.ReserveAmmo}";
        }

        private void OnHit(IDamageable target, bool headshot)
        {
            if (hitmarker == null) return;
            hitmarker.alpha = 1f;
        }

        private void OnScoreChanged()
        {
            if (killsLabel != null) killsLabel.text = game.Kills.ToString();
            if (scoreLabel != null) scoreLabel.text = game.Score.ToString();
        }

        private void OnWaveStarted(int wave)
        {
            if (waveLabel != null) waveLabel.text = $"WAVE {wave}";
        }

        private void OnPlayerDied()
        {
            if (damageVignette != null) damageVignette.alpha = 1f;
        }

        private void OnGameOver()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(true);

            // The controls go with the player. They are full-screen raycast targets — the look
            // area especially — and leaving them live means a dead player mashing a FIRE button
            // that does nothing, on top of the LOBBY button that would get them out.
            if (touchControls != null) touchControls.SetActive(false);

            // The XP alongside the stats, because that is the number that turns a death into
            // progress, and progress is the only reason anyone presses PLAY AGAIN.
            int wave = game.Spawner.WaveNumber;

            if (gameOverStats != null)
                gameOverStats.text =
                    $"WAVE {wave}   ·   {game.Kills} KILLS   ·   {game.Score} POINTS   " +
                    $"·   +{Game.Core.PlayerProfile.XpFor(game.Kills, wave)} XP";

            if (damageVignette != null) damageVignette.alpha = 0f;
        }

        /// <summary>Hooked to PLAY AGAIN in the game-over panel.</summary>
        public void OnRestartPressed() => game.Restart();

        /// <summary>Hooked to LOBBY in the game-over panel. The match is already banked by then;
        /// this just leaves.</summary>
        public void OnLobbyPressed() => game.QuitToLobby();
    }
}
