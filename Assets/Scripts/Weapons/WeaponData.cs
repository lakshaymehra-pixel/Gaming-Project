using UnityEngine;

namespace Game.Weapons
{
    public enum FireMode { Single, Auto, Burst }

    /// <summary>
    /// Tuning for one gun, authored as an asset so weapons can be balanced without
    /// touching code and new guns are a right-click away.
    /// Create via: Assets > Create > Game > Weapon Data
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Game/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Rifle";

        [Header("Fire")]
        public FireMode fireMode = FireMode.Auto;
        [Tooltip("Rounds per minute.")]
        public float fireRate = 600f;
        public int burstCount = 3;

        [Header("Damage")]
        public float damage = 22f;
        public float headshotMultiplier = 2f;
        public float range = 120f;

        [Header("Accuracy (degrees of cone half-angle)")]
        public float hipSpread = 2.2f;
        public float aimSpread = 0.4f;
        [Tooltip("Extra spread added per shot while holding the trigger.")]
        public float spreadPerShot = 0.35f;
        public float maxBloom = 5f;
        public float spreadRecovery = 6f;

        [Header("Recoil (degrees)")]
        public float recoilVertical = 1.1f;
        public float recoilHorizontal = 0.35f;
        public float recoilRecovery = 9f;

        [Header("Ammo")]
        public int magazineSize = 30;
        public int reserveAmmo = 180;
        public float reloadSeconds = 2f;

        /// <summary>Seconds between rounds, derived from RPM.</summary>
        public float SecondsBetweenShots => fireRate <= 0f ? 0.1f : 60f / fireRate;
    }
}
