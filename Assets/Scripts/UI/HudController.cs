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
            if (gameOverStats != null)
                gameOverStats.text =
                    $"WAVE {game.Spawner.WaveNumber}\n" +
                    $"{game.Kills} KILLS\n" +
                    $"{game.Score} POINTS";
        }

        /// <summary>Hooked to the Restart button in the game-over panel.</summary>
        public void OnRestartPressed() => game.Restart();
    }
}
