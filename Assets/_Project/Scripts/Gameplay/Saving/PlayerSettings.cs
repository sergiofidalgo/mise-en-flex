using System;
using System.IO;
using Game.Data;
using UnityEngine;

namespace Game.Gameplay.Saving
{
    /// <summary>
    /// The players' settings, shared by every slot (decision 2026-11-08): for now the three realism dials
    /// that exist. Presets and rebinding come with Phase 5's realism settings menu.
    /// </summary>
    [Serializable]
    public sealed class PlayerSettings
    {
        private const string FileName = "settings.json";

        public float CookingPrecision = -1f;
        public float Spoilage = -1f;
        public float Patience = -1f;

        private static string Path => System.IO.Path.Combine(SaveSlots.Root, FileName);

        /// <summary>The saved settings, with the designer's defaults for anything never set.</summary>
        public static PlayerSettings Load(RealismSettings defaults)
        {
            PlayerSettings settings = null;
            try
            {
                if (File.Exists(Path))
                    settings = JsonUtility.FromJson<PlayerSettings>(File.ReadAllText(Path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Couldn't read the settings ({exception.Message}); using the defaults.");
            }
            settings ??= new PlayerSettings();
            if (settings.CookingPrecision < 0f)
                settings.CookingPrecision = defaults.DefaultCookingPrecision;
            if (settings.Spoilage < 0f)
                settings.Spoilage = defaults.DefaultSpoilageMultiplier;
            if (settings.Patience < 0f)
                settings.Patience = defaults.DefaultCustomerPatience;
            return settings;
        }

        public void Save()
        {
            Directory.CreateDirectory(SaveSlots.Root);
            File.WriteAllText(Path, JsonUtility.ToJson(this, true));
        }

        public void ApplyTo(RealismSettings realism) => realism.UsePlayerValues(CookingPrecision, Spoilage, Patience);
    }
}
