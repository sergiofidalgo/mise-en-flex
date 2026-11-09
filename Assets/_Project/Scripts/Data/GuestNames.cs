using System.Collections.Generic;
using Game.Simulation.Saving;

namespace Game.Data
{
    /// <summary>
    /// Guests' first names per language, slot for slot (user's call, 2026-11-09): the same guest is
    /// "Mike S." in English and "Miguel S." in Spanish, so a translated game gets local names rather
    /// than translated text with English names in it. Saves keep a guest's slot and initial, and the
    /// name is looked up in the current language when shown.
    /// Each language must fill every slot (GuestNameTests). The names are everyday ones for the
    /// language, not literal translations; a slot is a role (a guy called Mike, a woman called Megan).
    /// </summary>
    public static class GuestNames
    {
        /// <summary>The language names are shown in: the game's (Locale), so names and money always agree.</summary>
        public static string Language
        {
            get => Locale.Language;
            set => Locale.Language = value;
        }

        private static readonly Dictionary<string, string[]> Tables = new Dictionary<string, string[]>
        {
            // An American neighbourhood's diners (the setting, art bible 2026-11-08).
            ["en"] = new[]
            {
                "Mike", "Jessica", "Tyler", "Ashley", "Brandon", "Megan", "Chris", "Lauren", "Dave", "Brittany",
                "Jamal", "Keisha", "Carlos", "Maria", "Tony", "Gina", "Bobby", "Linda", "Hank", "Dolores",
                "Trey", "Destiny", "Luis", "Priya", "Kenny", "Shawna", "Earl", "Becky", "Kevin", "Tamika",
            },
            // The same diners for a Spanish-speaking player.
            ["es"] = new[]
            {
                "Miguel", "Lucía", "Hugo", "Andrea", "Bruno", "Marta", "Cristian", "Laura", "David", "Paula",
                "Jaime", "Carla", "Carlos", "María", "Toni", "Gema", "Roberto", "Elena", "Paco", "Dolores",
                "Iker", "Alba", "Luis", "Nerea", "Kike", "Noelia", "Ramón", "Rebeca", "Kevin", "Yaiza",
            },
        };

        public static IReadOnlyDictionary<string, string[]> All => Tables;

        /// <summary>How many guests' names each language has.</summary>
        public static int Slots => Tables["en"].Length;

        /// <summary>A slot's first name in the current language, or English if that language has no table.</summary>
        public static string First(int slot, string language = null)
        {
            if (!Tables.TryGetValue(language ?? Language, out var names))
                names = Tables["en"];
            return names[((slot % names.Length) + names.Length) % names.Length];
        }

        /// <summary>
        /// A guest's name as the listing shows it, e.g. "Mike S.", in the current language. Reviews
        /// saved before names had slots show the name they were saved with.
        /// </summary>
        public static string Display(ReviewRecord review, string language = null) =>
            review.GuestSlot >= 0 ? $"{First(review.GuestSlot, language)} {review.GuestInitial}." : review.Guest;

        /// <summary>The first name alone, for lines that address the guest.</summary>
        public static string FirstOf(ReviewRecord review, string language = null) =>
            review.GuestSlot >= 0 ? First(review.GuestSlot, language) : (review.Guest ?? "").Split(' ')[0];
    }
}
