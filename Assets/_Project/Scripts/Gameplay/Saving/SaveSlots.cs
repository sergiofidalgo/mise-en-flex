using System.Collections.Generic;
using System.IO;
using Game.Simulation.Saving;
using UnityEngine;

namespace Game.Gameplay.Saving
{
    /// <summary>
    /// The three campaign slots (decision 2026-11-08), each its own folder with a campaign file and the
    /// photo taken at the last close.
    /// </summary>
    public static class SaveSlots
    {
        public const int Count = 3;

        private const string PhotoName = "photo.jpg";

        /// <summary>Where all saves live: the persistent data path, or a test's throwaway folder.</summary>
        public static string Root => SaveFile.DirectoryOverride ?? Application.persistentDataPath;

        public static string Folder(int slot) => Path.Combine(Root, $"slot{slot + 1}");

        public static SaveFile File(int slot)
        {
            MoveSingleCampaignIntoSlotOne();
            return new SaveFile(Folder(slot));
        }

        public static string PhotoPath(int slot) => Path.Combine(Folder(slot), PhotoName);

        /// <summary>Each slot's campaign, or null where the slot is free (or its file can't be read).</summary>
        public static CampaignSave[] ReadAll()
        {
            var saves = new CampaignSave[Count];
            for (var i = 0; i < Count; i++)
                saves[i] = File(i).Read();
            return saves;
        }

        /// <summary>The slot played most recently, or -1 with none.</summary>
        public static int LastPlayed(IReadOnlyList<CampaignSave> saves)
        {
            var best = -1;
            for (var i = 0; i < saves.Count; i++)
            {
                if (saves[i] != null && (best < 0 || saves[i].LastPlayedTicks > saves[best].LastPlayedTicks))
                    best = i;
            }
            return best;
        }

        /// <summary>The first slot with nothing in it, or -1 when all are taken.</summary>
        public static int FirstFree(IReadOnlyList<CampaignSave> saves)
        {
            for (var i = 0; i < saves.Count; i++)
            {
                if (saves[i] == null)
                    return i;
            }
            return -1;
        }

        /// <summary>Empties a slot for good: its campaign and photo.</summary>
        public static void Delete(int slot)
        {
            var folder = Folder(slot);
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        // Before slots there was one campaign.json beside them: it becomes slot 1, once.
        private static void MoveSingleCampaignIntoSlotOne()
        {
            var old = new SaveFile(Root);
            var first = new SaveFile(Folder(0));
            if (!old.Exists || first.Exists)
                return;
            Directory.CreateDirectory(Folder(0));
            System.IO.File.Move(old.Path, first.Path);
        }
    }
}
