using System;
using System.Collections.Generic;

namespace Game.Simulation.Saving
{
    /// <summary>
    /// Everything that lasts from one day to the next, written as Review starts (decision 2026-11-06).
    /// Plain data with public fields so any JSON writer can handle it; the gameplay layer writes the
    /// file. Every field has a default, so a file from an older version loads with sensible values
    /// for what it didn't have, and <see cref="SaveMigration"/> fixes anything a default can't.
    /// <para>
    /// A campaign is an empire of restaurants (Phase 6, decision 2026-11-10). What belongs to the
    /// people (the money, the loan, the menu they've unlocked, the time they've put in) is kept here
    /// once; what belongs to a place (its room, stock, mess, guests and fee clock) is kept per
    /// restaurant in <see cref="Restaurants"/>. Only the current one runs; the others wait as they
    /// were left.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class CampaignSave
    {
        /// <summary>
        /// Bumped whenever the shape changes in a way a default can't cover.
        /// 1: one restaurant, its fields at the top. 2: a list of restaurants (Phase 6).
        /// </summary>
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;

        /// <summary>
        /// Can be negative: money owed to the boss (decision 2026-11-08). The campaign's, not a
        /// restaurant's: every restaurant pays into and out of the same pocket.
        /// </summary>
        public float Balance;

        /// <summary>
        /// What the accountant has lent and is still to be paid back (decision 2026-11-10: the loan
        /// belongs to the campaign, and each collection takes one instalment), the boss's 20% included.
        /// </summary>
        public float Loan;

        // ---- The boss and money trouble (Phase 6, milestone 4). New fields with defaults: an older
        // file loads with no loan and nothing owed beyond its balance. ----

        /// <summary>What each collection takes off <see cref="Loan"/>; 0 with no loan.</summary>
        public float LoanInstalment;

        /// <summary>How far the escalation has gone, a <see cref="Campaign.BossStep"/>: 0, nothing.</summary>
        public int BossStep;

        /// <summary>What the boss does the next morning, a <see cref="Campaign.BossVisit"/>: 0, nothing.</summary>
        public int BossVisit;

        /// <summary>The definition id of the restaurant he'll visit.</summary>
        public string BossVisitAt = "";

        /// <summary>The loan as the simulation works with it.</summary>
        public Campaign.LoanAccount ToLoan() => new Campaign.LoanAccount(Loan, LoanInstalment);

        public void StoreLoan(Campaign.LoanAccount loan)
        {
            Loan = loan.Owed;
            LoanInstalment = loan.Instalment;
        }

        /// <summary>The escalation as the simulation works with it.</summary>
        public Campaign.BossEscalation ToEscalation() =>
            new Campaign.BossEscalation((Campaign.BossStep)BossStep, (Campaign.BossVisit)BossVisit, BossVisitAt);

        public void StoreEscalation(Campaign.BossEscalation escalation)
        {
            BossStep = (int)escalation.Step;
            BossVisit = (int)escalation.Pending;
            BossVisitAt = escalation.PendingAt;
        }

        /// <summary>
        /// The boss takes a restaurant (§15.3 step 3): its record goes, and if it was the current one,
        /// the first left becomes current. False if it isn't owned or is the last one.
        /// </summary>
        public bool Remove(string definitionId)
        {
            if (Restaurants == null || Restaurants.Count <= 1)
                return false;
            var index = Restaurants.FindIndex(r => r.DefinitionId == definitionId);
            if (index < 0)
                return false;
            Restaurants.RemoveAt(index);
            if (CurrentRestaurant == index)
                CurrentRestaurant = 0;
            else if (CurrentRestaurant > index)
                CurrentRestaurant--;
            return true;
        }

        /// <summary>Real seconds played in this campaign, in any restaurant.</summary>
        public float PlayedSeconds;

        /// <summary>When it was last saved, as <see cref="DateTime.Ticks"/> in local time.</summary>
        public long LastPlayedTicks;

        /// <summary>Every player number that has cooked in this campaign, for the card's coloured dots.</summary>
        public List<int> Cooks = new List<int>();

        /// <summary>
        /// Review and reply lines used lately, so they don't repeat across days. The campaign's: the
        /// same players read every restaurant's listing, so a line shouldn't repeat across them either.
        /// </summary>
        public List<string> RecentLines = new List<string>();

        /// <summary>
        /// Cookbook recipes this campaign has unlocked, its menu (decision 2026-11-10). The campaign's:
        /// a recipe is bought once with the campaign's money and can be cooked anywhere. Empty in a
        /// file from before the cookbook: the starter recipes are unlocked anyway.
        /// </summary>
        public List<string> Recipes = new List<string>();

        /// <summary>Index into <see cref="Restaurants"/> of the one being played; -1 with none.</summary>
        public int CurrentRestaurant = -1;

        /// <summary>Every restaurant owned, in the order they were bought.</summary>
        public List<RestaurantSave> Restaurants = new List<RestaurantSave>();

        /// <summary>The restaurant being played, or null if there is none yet.</summary>
        public RestaurantSave Current =>
            Restaurants != null && CurrentRestaurant >= 0 && CurrentRestaurant < Restaurants.Count ? Restaurants[CurrentRestaurant] : null;

        /// <summary>The restaurant made from that definition, or null if it isn't owned.</summary>
        public RestaurantSave Find(string definitionId) =>
            Restaurants?.Find(r => r.DefinitionId == definitionId);

        /// <summary>Adds a restaurant (it doesn't become current) and returns it.</summary>
        public RestaurantSave Add(RestaurantSave restaurant)
        {
            Restaurants ??= new List<RestaurantSave>();
            Restaurants.Add(restaurant);
            if (CurrentRestaurant < 0)
                CurrentRestaurant = 0;
            return restaurant;
        }

        /// <summary>Makes the restaurant at <paramref name="index"/> the one played next.</summary>
        public void Switch(int index)
        {
            if (Restaurants == null || index < 0 || index >= Restaurants.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            CurrentRestaurant = index;
        }

        /// <summary>
        /// Puts a freshly captured record in place of the current restaurant's, leaving every other
        /// restaurant exactly as it was: idle places don't change while nobody is in them.
        /// </summary>
        public void StoreCurrent(RestaurantSave restaurant)
        {
            if (Current == null)
            {
                Add(restaurant);
                CurrentRestaurant = Restaurants.Count - 1;
                return;
            }
            Restaurants[CurrentRestaurant] = restaurant;
        }
    }

    /// <summary>One patch of dirt (see <see cref="Cleaning.DirtSpot"/>).</summary>
    [Serializable]
    public sealed class DirtRecord
    {
        public int Kind;
        public int Surface;

        /// <summary>The table or counter it's on, as an index into <see cref="RestaurantSave.Stations"/>; -1 on the floor.</summary>
        public int Station = -1;

        /// <summary>In the room on the floor; from the station's centre, in its own axes, on a station.</summary>
        public float X;
        public float Z;

        public float Amount;
        public float Peak;
    }

    [Serializable]
    public sealed class DishCount
    {
        /// <summary>As orders are written, e.g. "steak, medium".</summary>
        public string Dish = "";
        public int Count;
    }

    /// <summary>One piece of furniture where it stands.</summary>
    [Serializable]
    public sealed class StationRecord
    {
        /// <summary>Its furniture catalogue id: rebuilt from the catalogue. Empty for pieces that aren't for sale.</summary>
        public string FurnitureId = "";

        /// <summary>For pieces that aren't for sale (the sink, the bin): which one in the scene it is.</summary>
        public string SceneName = "";

        public float X;
        public float Z;
        public float Degrees;

        /// <summary>Chairs at a table.</summary>
        public int Chairs;

        /// <summary>Things counted rather than kept, such as what's in the bin.</summary>
        public int Count;
    }

    public enum ItemKind
    {
        Plain,
        Ingredient,
        Pan,
        Plate,
        Knife,
        Tray,
        Container,
    }

    /// <summary>
    /// An item and where it is: on a station (<see cref="Station"/>, in place <see cref="Slot"/>),
    /// inside another item (<see cref="Parent"/>), or loose (both -1, e.g. it was in someone's hands).
    /// Records come parents first, and a container's contents in the order they were put in.
    /// </summary>
    [Serializable]
    public sealed class ItemRecord
    {
        public int Station = -1;
        public int Slot;
        public int Parent = -1;
        public string TypeId = "";
        public ItemKind Kind;

        /// <summary>Plates.</summary>
        public bool Dirty;

        /// <summary>Pans: °C.</summary>
        public float Temperature;

        /// <summary>Only meaningful when <see cref="Kind"/> is <see cref="ItemKind.Ingredient"/>.</summary>
        public IngredientState Ingredient = new IngredientState();
    }

    /// <summary>All of an ingredient's cooking state: enough to carry on exactly where it was.</summary>
    [Serializable]
    public sealed class IngredientState
    {
        public float[] Temperatures = new float[3];
        public float[] PeakTemperatures = new float[3];
        public float[] Browning = new float[2];
        public float Freshness = 1f;
        public float Seasoning;
        public bool WasCut;
        public float CutEvenness;
        public bool SideBDown;
        public bool Flipped;
        public float SecondsOutOfPan;
        public bool HasEnvironment;
        public float EnvironmentTemperature;
    }

    [Serializable]
    public sealed class StaffRecord
    {
        public string Name = "";
        public int Role;
        public string Description = "";
        public float Skill;
        public float Speed;
        public float DailyWage;
        public float Morale = -1f;
    }

    /// <summary>One review in the listing, with everything its card shows.</summary>
    [Serializable]
    public sealed class ReviewRecord
    {
        public int Day;
        public float Stars;
        public int Topic;
        public string Line = "";

        /// <summary>The guest's name in English, e.g. "Mike S."; also how the guest book knows them.</summary>
        public string Guest = "";

        /// <summary>Which name the guest has, so it can be shown in any language; -1 in older saves.</summary>
        public int GuestSlot = -1;

        /// <summary>The guest's last initial, e.g. "S".</summary>
        public string GuestInitial = "";

        /// <summary>What they ordered, e.g. "steak, medium"; empty if they never ordered.</summary>
        public string Order = "";

        /// <summary>Service seconds when they arrived.</summary>
        public float ArrivedAt;

        public bool WalkedOut;
        public float Paid;
        public float Tip;

        /// <summary>What walking out cost.</summary>
        public float Penalty;

        /// <summary>The reply given (a <see cref="Service.ReviewReply"/>), or -1 for none yet.</summary>
        public int Reply = -1;

        /// <summary>Stars this review counts for in the rating, after the reply's nudge.</summary>
        public float RatedStars => Math.Max(0f, Math.Min(5f, Stars + Nudge));

        /// <summary>What the reply did to how this review counts.</summary>
        public float Nudge;

        /// <summary>Who wrote it (a <see cref="Service.Persona"/>).</summary>
        public int Persona;

        /// <summary>The owner's reply as posted; empty for a reply from before replies were written per review.</summary>
        public string ReplyText = "";

        /// <summary>
        /// The templates the review's line was written from, in order (decision 2026-11-09): with
        /// <see cref="Facts"/>, the line is written again in the player's language when shown. Empty
        /// for lines that couldn't be traced, which show <see cref="Line"/> as saved.
        /// </summary>
        public List<string> LineParts = new List<string>();

        /// <summary>The templates the owner's reply was written from: its greeting, heart and sign-off.</summary>
        public List<string> ReplyParts = new List<string>();

        /// <summary>The facts the review and reply were filled with, in English.</summary>
        public Service.ReviewFacts Facts = new Service.ReviewFacts();

        /// <summary>The owner's mood when they replied (a <see cref="Service.OwnerMood"/>): 0, fresh, for older saves.</summary>
        public int ReplyMood;

        /// <summary>Which visit this was for the guest: 1 (or 0, in older saves) the first.</summary>
        public int Visit;
    }

    /// <summary>Brings a file from an older version up to date, one version at a time.</summary>
    public static class SaveMigration
    {
        /// <param name="version1">
        /// For a version-1 file: the same text read as <see cref="Version1Restaurant"/>, since its
        /// restaurant's fields sit at the top of the file where version 2 no longer looks. The
        /// gameplay layer reads it (it has the JSON reader); null leaves restaurant 1 empty.
        /// </param>
        /// <returns>False if the file is from a newer version of the game, which can't be read safely.</returns>
        public static bool Upgrade(CampaignSave save, Version1Restaurant version1 = null)
        {
            if (save == null || save.Version > CampaignSave.CurrentVersion)
                return false;

            // Each step takes version n to n + 1.
            while (save.Version < CampaignSave.CurrentVersion)
            {
                switch (save.Version)
                {
                    case 1:
                        OneRestaurantBecomesTheFirst(save, version1 ?? new Version1Restaurant());
                        break;
                }
                save.Version++;
            }

            // Lists a hand-edited or partial file left out.
            save.Restaurants ??= new List<RestaurantSave>();
            save.Cooks ??= new List<int>();
            save.RecentLines ??= new List<string>();
            save.Recipes ??= new List<string>();
            save.BossVisitAt ??= "";
            if (save.CurrentRestaurant >= save.Restaurants.Count || (save.CurrentRestaurant < 0 && save.Restaurants.Count > 0))
                save.CurrentRestaurant = save.Restaurants.Count > 0 ? 0 : -1;
            return true;
        }

        // 1 → 2 (Phase 6): the one restaurant of a version-1 file becomes restaurant 1 of the empire,
        // and the current one. The campaign's own fields (balance, play time, cooks, recent lines,
        // recipes) kept their names, so they've already been read into place. The loan is new: 0.
        private static void OneRestaurantBecomesTheFirst(CampaignSave save, Version1Restaurant old)
        {
            save.Restaurants = new List<RestaurantSave> { old.ToRestaurant() };
            save.CurrentRestaurant = 0;
        }
    }
}
