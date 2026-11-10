using System;
using System.IO;
using Game.Data;
using Game.Simulation.Realism;
using UnityEngine;

namespace Game.Gameplay.Saving
{
    /// <summary>
    /// The players' settings, shared by every slot (decision 2026-11-08): the five realism dials and the
    /// language, changed in the pause menu (decision 2026-11-10). The preset isn't stored; it's read
    /// back from the dials. A dial missing from an older file (-1) takes the designer's default.
    /// </summary>
    [Serializable]
    public sealed class PlayerSettings
    {
        private const string FileName = "settings.json";

        public float CookingPrecision = -1f;
        public float Spoilage = -1f;
        public float Patience = -1f;
        public float MinigameComplexity = -1f;
        public float Hygiene = -1f;

        /// <summary>The language the game is shown in, by code (decision 2026-11-09).</summary>
        public string Language = "en";

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
            settings.FillMissing(defaults.Defaults);
            return settings;
        }

        /// <summary>Gives any dial never set (-1, an older file) the designer's default.</summary>
        public void FillMissing(RealismDials defaults)
        {
            if (CookingPrecision < 0f)
                CookingPrecision = defaults.CookingPrecision;
            if (Spoilage < 0f)
                Spoilage = defaults.Spoilage;
            if (Patience < 0f)
                Patience = defaults.Patience;
            if (MinigameComplexity < 0f)
                MinigameComplexity = defaults.MinigameComplexity;
            if (Hygiene < 0f)
                Hygiene = defaults.Hygiene;
        }

        /// <summary>The five dials, as the simulation reads them.</summary>
        public RealismDials Dials
        {
            get => new RealismDials
            {
                CookingPrecision = CookingPrecision,
                Spoilage = Spoilage,
                Patience = Patience,
                MinigameComplexity = MinigameComplexity,
                Hygiene = Hygiene,
            };
            set
            {
                CookingPrecision = value.CookingPrecision;
                Spoilage = value.Spoilage;
                Patience = value.Patience;
                MinigameComplexity = value.MinigameComplexity;
                Hygiene = value.Hygiene;
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(SaveSlots.Root);
            File.WriteAllText(Path, JsonUtility.ToJson(this, true));
        }

        public void ApplyTo(RealismSettings realism)
        {
            realism.UsePlayerValues(Dials);
            Locale.Language = string.IsNullOrEmpty(Language) ? "en" : Language;
        }
    }
}
