using System;
using System.IO;
using Game.Simulation.Saving;
using UnityEngine;

namespace Game.Gameplay.Saving
{
    /// <summary>
    /// The campaign file: JSON in <see cref="Application.persistentDataPath"/>, one campaign at a time
    /// with no slots (decision 2026-11-06). Written to a temporary file first and then swapped in,
    /// so quitting mid-write never leaves a broken save.
    /// </summary>
    public sealed class SaveFile
    {
        private const string FileName = "campaign.json";
        private const string PreviousName = "campaign.previous.json";

        private readonly string _directory;

        /// <summary>
        /// Tests set this so playing a day in a test never touches the player's real campaign.
        /// </summary>
        public static string DirectoryOverride { get; set; }

        /// <param name="directory">Where the file lives; the persistent data path unless a test says otherwise.</param>
        public SaveFile(string directory = null)
        {
            _directory = directory ?? DirectoryOverride ?? Application.persistentDataPath;
        }

        public string Path => System.IO.Path.Combine(_directory, FileName);

        public bool Exists => File.Exists(Path);

        public void Write(CampaignSave save)
        {
            Directory.CreateDirectory(_directory);
            var temporary = Path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(save, true));
            if (File.Exists(Path))
                File.Delete(Path);
            File.Move(temporary, Path);
        }

        /// <summary>The saved campaign, brought up to date; null if there isn't one or it can't be read.</summary>
        public CampaignSave Read()
        {
            if (!Exists)
                return null;
            try
            {
                var save = Parse(File.ReadAllText(Path));
                if (save != null)
                    return save;
                Debug.LogWarning($"The save at {Path} is from a newer version of the game; starting a new campaign instead.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"The save at {Path} couldn't be read ({exception.Message}); starting a new campaign instead.");
            }
            return null;
        }

        /// <summary>
        /// A campaign from the file's text, brought up to date; null if it's from a newer game. A
        /// version-1 file is read twice: once as the campaign, once for the restaurant whose fields sat
        /// at its top (they become restaurant 1).
        /// </summary>
        public static CampaignSave Parse(string json)
        {
            var save = JsonUtility.FromJson<CampaignSave>(json);
            var version1 = save != null && save.Version == 1 ? JsonUtility.FromJson<Version1Restaurant>(json) : null;
            return SaveMigration.Upgrade(save, version1) ? save : null;
        }

        /// <summary>
        /// A new campaign replaces the old one. The old file is kept aside as a backup rather than
        /// deleted, in case it was a mistake; nothing in the game reads it.
        /// </summary>
        public void Retire()
        {
            if (!Exists)
                return;
            var previous = System.IO.Path.Combine(_directory, PreviousName);
            if (File.Exists(previous))
                File.Delete(previous);
            File.Move(Path, previous);
        }
    }
}
