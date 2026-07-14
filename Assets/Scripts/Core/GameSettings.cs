using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Per-device preferences: volume, look sensitivity, how hard the phone is allowed to work.
    ///
    /// PlayerPrefs here is not a compromise the way it is for the profile — it is correct.
    /// Graphics settings belong to the phone, not to the account: nobody wants the tier they
    /// picked on a flagship to follow them onto a budget handset and drop it to four frames.
    /// </summary>
    public static class GameSettings
    {
        /// <summary>
        /// What the phone is asked to do. The names are the ones players already know from
        /// every other mobile shooter, and they mean the same thing here.
        /// </summary>
        public enum Tier { Smooth = 0, Balanced = 1, HD = 2 }

        private const string KeyVolume = "set_volume";
        private const string KeySensitivity = "set_sensitivity";
        private const string KeyTier = "set_tier";
        private const string KeyFoliage = "set_foliage";

        // Defaults are the middle of the road: Balanced, and the sensitivity the player rig was
        // originally tuned at, so an untouched settings screen changes nothing.
        public static float Volume
        {
            get => PlayerPrefs.GetFloat(KeyVolume, 0.8f);
            set => PlayerPrefs.SetFloat(KeyVolume, Mathf.Clamp01(value));
        }

        /// <summary>0..1 on the slider. Applied as a multiplier on the rig's own values, so the
        /// midpoint is what the game was tuned at rather than an arbitrary number.</summary>
        public static float Sensitivity
        {
            get => PlayerPrefs.GetFloat(KeySensitivity, 0.5f);
            set => PlayerPrefs.SetFloat(KeySensitivity, Mathf.Clamp01(value));
        }

        public static Tier Graphics
        {
            get => (Tier)PlayerPrefs.GetInt(KeyTier, (int)Tier.Balanced);
            set => PlayerPrefs.SetInt(KeyTier, (int)value);
        }

        /// <summary>
        /// How much of the undergrowth is drawn, 0..1. Only the layers with no collider are ever
        /// culled — leaves, ferns, fronds — so this cannot change what you can hide behind, what
        /// you can shoot through, or where an enemy can walk. That constraint is why it is safe
        /// to hand to a slider at all.
        /// </summary>
        public static float Foliage
        {
            get => PlayerPrefs.GetFloat(KeyFoliage, 1f);
            set => PlayerPrefs.SetFloat(KeyFoliage, Mathf.Clamp01(value));
        }

        public static void Save() => PlayerPrefs.Save();

        /// <summary>
        /// The multiplier a look rig applies to its own tuned sensitivity. Half the slider is
        /// 1x — the value the game was built at — and the ends are half and double it.
        /// </summary>
        public static float SensitivityScale => Mathf.Lerp(0.5f, 2f, Sensitivity);

        /// <summary>
        /// Pushes the settings at Unity. Called from Awake by SettingsApplier, in every scene,
        /// because a setting that only takes effect in the scene you changed it in is not a
        /// setting, it is a lie with a slider on it.
        /// </summary>
        public static void Apply()
        {
            AudioListener.volume = Volume;

            switch (Graphics)
            {
                case Tier.Smooth:
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.shadowDistance = 0f;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.globalTextureMipmapLimit = 1;   // half-res textures
                    Application.targetFrameRate = 60;
                    break;

                case Tier.Balanced:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowDistance = 35f;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.globalTextureMipmapLimit = 0;
                    Application.targetFrameRate = 60;
                    break;

                case Tier.HD:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowDistance = 70f;
                    QualitySettings.antiAliasing = 2;
                    QualitySettings.globalTextureMipmapLimit = 0;
                    Application.targetFrameRate = 60;
                    break;
            }
        }
    }
}
