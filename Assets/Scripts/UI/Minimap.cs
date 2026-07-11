using System.Collections.Generic;
using Game.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Top-down radar. A second camera renders the world into a RenderTexture for the
    /// backdrop, and enemy blips are drawn as UI icons on top of it rather than as world
    /// objects — a blip must stay the same size on screen no matter how far the camera is
    /// pulled back, and must clamp to the rim when its enemy is off-radar, neither of which
    /// a rendered object can do.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private RectTransform mapRect;
        [SerializeField] private RectTransform playerArrow;
        [SerializeField] private RectTransform blipContainer;
        [SerializeField] private Image blipPrefab;

        [Header("Radar")]
        [Tooltip("World radius the radar covers, in metres.")]
        [SerializeField] private float range = 70f;
        [SerializeField] private float cameraHeight = 90f;

        [Tooltip("Keep off-radar enemies pinned to the rim instead of hiding them.")]
        [SerializeField] private bool clampToEdge = true;

        [Header("Rescan")]
        [Tooltip("Seconds between sweeps for new enemies. Not every frame — the scene query "
               + "is the expensive part, and enemies do not spawn that fast.")]
        [SerializeField] private float rescanInterval = 0.5f;

        private readonly List<EnemyAI> _enemies = new();
        private readonly List<Image> _blips = new();
        private float _nextRescan;
        private float _radiusPixels;

        private void Start()
        {
            if (player == null || minimapCamera == null || mapRect == null)
            {
                Debug.LogError($"{name}: minimap is missing references.", this);
                enabled = false;
                return;
            }

            _radiusPixels = mapRect.rect.width * 0.5f;

            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = range;
            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void LateUpdate()
        {
            // Follow the player from directly above, but do not inherit their yaw: a
            // north-up radar is easier to read than one that spins under you.
            Vector3 p = player.position;
            minimapCamera.transform.position = new Vector3(p.x, p.y + cameraHeight, p.z);

            // The arrow rotates instead, showing facing against a fixed world.
            if (playerArrow != null)
                playerArrow.localRotation = Quaternion.Euler(
                    0f, 0f, -player.eulerAngles.y);

            if (Time.time >= _nextRescan)
            {
                _nextRescan = Time.time + rescanInterval;
                Rescan();
            }

            DrawBlips();
        }

        private void Rescan()
        {
            _enemies.Clear();

            foreach (EnemyAI e in Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            {
                if (e != null && !e.Health.IsDead) _enemies.Add(e);
            }
        }

        private void DrawBlips()
        {
            EnsureBlipCount(_enemies.Count);

            for (int i = 0; i < _blips.Count; i++)
            {
                if (i >= _enemies.Count || _enemies[i] == null || _enemies[i].Health.IsDead)
                {
                    _blips[i].gameObject.SetActive(false);
                    continue;
                }

                Vector3 offset = _enemies[i].transform.position - player.position;
                var flat = new Vector2(offset.x, offset.z);

                float distance = flat.magnitude;
                bool offRadar = distance > range;

                if (offRadar && !clampToEdge)
                {
                    _blips[i].gameObject.SetActive(false);
                    continue;
                }

                // Map world metres onto radar pixels, then clamp so a distant enemy sticks to
                // the rim rather than sliding off the map.
                Vector2 position = flat / range * _radiusPixels;
                if (position.magnitude > _radiusPixels)
                    position = position.normalized * _radiusPixels;

                _blips[i].rectTransform.anchoredPosition = position;
                _blips[i].gameObject.SetActive(true);

                // Rim blips are dimmed, so "somewhere out that way" does not read the same as
                // "right there".
                Color c = _blips[i].color;
                c.a = offRadar ? 0.45f : 1f;
                _blips[i].color = c;
            }
        }

        private void EnsureBlipCount(int wanted)
        {
            while (_blips.Count < wanted)
            {
                Image blip = Instantiate(blipPrefab, blipContainer);
                blip.gameObject.SetActive(false);
                _blips.Add(blip);
            }
        }
    }
}
