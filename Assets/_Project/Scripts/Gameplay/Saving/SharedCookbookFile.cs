using System;
using System.IO;
using Game.Simulation.Cookbook;
using UnityEngine;

namespace Game.Gameplay.Saving
{
    /// <summary>
    /// Every recipe any campaign has unlocked, on disk (decision 2026-11-10): <c>cookbook.json</c>
    /// beside the settings and the chefs, outside the campaign slots, so it outlives them. Each
    /// campaign still buys its own menu; this is what the Free kitchen sandbox will offer. Written at
    /// once whenever a campaign unlocks something new here.
    /// </summary>
    public static class SharedCookbookFile
    {
        private const string FileName = "cookbook.json";

        private static SharedCookbook _current;
        // A file from a newer game is never overwritten.
        private static bool _newerOnDisk;

        private static string Path => System.IO.Path.Combine(SaveSlots.Root, FileName);

        public static SharedCookbook Current => _current ??= Read();

        /// <summary>Adds a recipe a campaign just unlocked, and saves if it's new here.</summary>
        public static void Add(string recipeId)
        {
            if (Current.Add(recipeId))
                Save();
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
                Debug.LogWarning($"Couldn't save the cookbook ({exception.Message}).");
            }
        }

        /// <summary>Forgets what was read, so the next use reads the file again (tests, a changed save folder).</summary>
        public static void Reset()
        {
            _current = null;
            _newerOnDisk = false;
        }

        private static SharedCookbook Read()
        {
            try
            {
                if (File.Exists(Path))
                {
                    var book = JsonUtility.FromJson<SharedCookbook>(File.ReadAllText(Path));
                    if (book != null && book.Upgrade())
                        return book;
                    _newerOnDisk = book != null;
                    Debug.LogWarning("The cookbook was saved by a newer version of the game; not saving over it.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Couldn't read the cookbook ({exception.Message}); starting an empty one.");
            }
            return new SharedCookbook();
        }
    }
}
