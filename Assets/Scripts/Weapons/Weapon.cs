using System;
using System.Collections;
using Game.Core;
using UnityEngine;

namespace Game.Weapons
{
    /// <summary>
    /// Hitscan gun. Fires from the camera centre (not the muzzle) so what the crosshair
    /// covers is what gets hit — the muzzle is only used to draw the tracer from.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private WeaponData data;

        [Header("References")]
        [SerializeField] private Camera shootCamera;
        [SerializeField] private Transform muzzle;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Feedback")]
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip fireClip;
        [SerializeField] private AudioClip reloadClip;
        [SerializeField] private AudioClip emptyClip;
        [SerializeField] private GameObject impactPrefab;
        [SerializeField] private TracerPool tracerPool;

        [Header("Tags")]
        [SerializeField] private string headColliderTag = "Head";

        private float _nextFireTime;
        private float _bloom;
        private bool _triggerWasDown;

        public WeaponData Data => data;
        public int AmmoInMagazine { get; private set; }
        public int ReserveAmmo { get; private set; }
        public bool IsReloading { get; private set; }

        /// <summary>Recoil this gun wants applied to the view, in degrees (pitch, yaw).</summary>
        public event Action<Vector2> RecoilKick;
        public event Action AmmoChanged;
        public event Action<IDamageable, bool> Hit; // target, wasHeadshot

        private void Awake()
        {
            if (data == null)
            {
                Debug.LogError($"{name}: WeaponData is not assigned.", this);
                enabled = false;
                return;
            }

            AmmoInMagazine = data.magazineSize;
            ReserveAmmo = data.reserveAmmo;
        }

        private void Update()
        {
            // Bloom decays whenever the trigger is not adding to it.
            _bloom = Mathf.Max(0f, _bloom - data.spreadRecovery * Time.deltaTime);
        }

        /// <param name="triggerHeld">Raw trigger state this frame.</param>
        /// <param name="isAiming">Tightens the cone.</param>
        public void UpdateTrigger(bool triggerHeld, bool isAiming)
        {
            bool justPressed = triggerHeld && !_triggerWasDown;
            _triggerWasDown = triggerHeld;

            if (IsReloading || Time.time < _nextFireTime) return;

            bool wantsToFire = data.fireMode == FireMode.Auto ? triggerHeld : justPressed;
            if (!wantsToFire) return;

            if (AmmoInMagazine <= 0)
            {
                if (justPressed) PlayOneShot(emptyClip);
                _nextFireTime = Time.time + 0.2f;
                return;
            }

            if (data.fireMode == FireMode.Burst)
                StartCoroutine(FireBurst(isAiming));
            else
                FireOne(isAiming);
        }

        private IEnumerator FireBurst(bool isAiming)
        {
            _nextFireTime = Time.time + data.SecondsBetweenShots * data.burstCount + 0.15f;

            for (int i = 0; i < data.burstCount && AmmoInMagazine > 0 && !IsReloading; i++)
            {
                FireOne(isAiming, advanceCooldown: false);
                yield return new WaitForSeconds(data.SecondsBetweenShots);
            }
        }

        private void FireOne(bool isAiming, bool advanceCooldown = true)
        {
            AmmoInMagazine--;
            if (advanceCooldown) _nextFireTime = Time.time + data.SecondsBetweenShots;

            AmmoChanged?.Invoke();

            Vector3 origin = shootCamera.transform.position;
            Vector3 direction = ApplySpread(shootCamera.transform.forward, isAiming);

            _bloom = Mathf.Min(data.maxBloom, _bloom + data.spreadPerShot);

            Vector3 endPoint = origin + direction * data.range;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, data.range,
                                hitMask, QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;
                ResolveHit(hit);
            }

            if (muzzle != null && tracerPool != null)
                tracerPool.Spawn(muzzle.position, endPoint);

            if (muzzleFlash != null) muzzleFlash.Play();
            PlayOneShot(fireClip);

            RecoilKick?.Invoke(new Vector2(
                data.recoilVertical,
                UnityEngine.Random.Range(-data.recoilHorizontal, data.recoilHorizontal)));
        }

        private void ResolveHit(RaycastHit hit)
        {
            bool headshot = hit.collider.CompareTag(headColliderTag);

            // The collider that was struck may be a child of the object carrying Health,
            // so search upward rather than only on the collider itself.
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && !target.IsDead)
            {
                float amount = data.damage * (headshot ? data.headshotMultiplier : 1f);
                target.TakeDamage(amount, gameObject);
                Hit?.Invoke(target, headshot);
            }

            if (impactPrefab != null)
            {
                GameObject fx = Instantiate(
                    impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(fx, 2f);
            }
        }

        private Vector3 ApplySpread(Vector3 forward, bool isAiming)
        {
            float baseSpread = isAiming ? data.aimSpread : data.hipSpread;
            float cone = baseSpread + _bloom;
            if (cone <= 0f) return forward;

            // Random point inside a cone of half-angle `cone` around forward.
            Vector2 disc = UnityEngine.Random.insideUnitCircle * Mathf.Tan(cone * Mathf.Deg2Rad);
            Vector3 dir = forward
                        + shootCamera.transform.right * disc.x
                        + shootCamera.transform.up * disc.y;
            return dir.normalized;
        }

        public void TryReload()
        {
            if (IsReloading) return;
            if (AmmoInMagazine >= data.magazineSize) return;
            if (ReserveAmmo <= 0) return;

            StartCoroutine(ReloadRoutine());
        }

        private IEnumerator ReloadRoutine()
        {
            IsReloading = true;
            PlayOneShot(reloadClip);

            yield return new WaitForSeconds(data.reloadSeconds);

            int needed = data.magazineSize - AmmoInMagazine;
            int taken = Mathf.Min(needed, ReserveAmmo);
            AmmoInMagazine += taken;
            ReserveAmmo -= taken;

            IsReloading = false;
            _bloom = 0f;
            AmmoChanged?.Invoke();
        }

        public void AddReserveAmmo(int amount)
        {
            ReserveAmmo += amount;
            AmmoChanged?.Invoke();
        }

        /// <summary>Current cone half-angle in degrees. The HUD sizes the crosshair from this.</summary>
        public float CurrentSpread(bool isAiming) =>
            (isAiming ? data.aimSpread : data.hipSpread) + _bloom;

        private void PlayOneShot(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }
    }
}
