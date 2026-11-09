using System;
using System.IO;
using Game.Simulation.Characters;
using UnityEngine;

namespace Game.Gameplay.Saving
{
    /// <summary>
    /// The players' chefs on disk (decision 2026-11-09): <c>profile.json</c> beside the settings, not
    /// in a campaign slot, so a chef outlives every campaign. Read once and shared by every scene;
    /// the tailor writes it at once on every change, as anything done in the office saves.
    /// </summary>
    public static class ChefProfileFile
    {
        private const string FileName = "profile.json";

        private static ChefProfile _current;
        // A file from a newer game is never overwritten: changes then last only until quitting.
        private static bool _newerOnDisk;

        private static string Path => System.IO.Path.Combine(SaveSlots.Root, FileName);

        /// <summary>Raised with the player slot whose look just changed.</summary>
        public static event Action<int> LookChanged;

        /// <summary>The profile, read from disk the first time; a fresh one if there's none or it can't be read.</summary>
        public static ChefProfile Current
        {
            get
            {
                if (_current == null)
                    _current = Read();
                return _current;
            }
        }

        /// <summary>Wears <paramref name="look"/> as player <paramref name="slot"/>'s chef and saves.</summary>
        public static void SetLook(int slot, ChefLook look)
        {
            Current.SetLook(slot, look);
            Save();
            LookChanged?.Invoke(slot);
        }

        public static void Save()
        {
            if (_newerOnDisk)
                return;
            try
            {
                Directory.CreateDirectory(SaveSlots.Root);
                var temporary = Path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(Current, true));
                if (File.Exists(Path))
                    File.Delete(Path);
                File.Move(temporary, Path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Couldn't save the chefs ({exception.Message}).");
            }
        }

        /// <summary>Forgets what was read, so the next use reads the file again (tests, a changed save folder).</summary>
        public static void Reset()
        {
            _current = null;
            _newerOnDisk = false;
        }

        private static ChefProfile Read()
        {
            try
            {
                if (File.Exists(Path))
                {
                    var profile = JsonUtility.FromJson<ChefProfile>(File.ReadAllText(Path));
                    if (profile != null && profile.Upgrade())
                        return profile;
                    _newerOnDisk = profile != null;
                    Debug.LogWarning("The chefs were saved by a newer version of the game; using the default chefs without saving over them.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Couldn't read the chefs ({exception.Message}); starting from the default chefs.");
            }
            var fresh = new ChefProfile();
            fresh.Upgrade();
            return fresh;
        }
    }
}
