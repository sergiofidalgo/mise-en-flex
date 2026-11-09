using System;
using System.Collections.Generic;

namespace Game.Simulation.Saving
{
    /// <summary>
    /// Everything that lasts from one day to the next, written as Review starts (decision 2026-11-06).
    /// Plain data with public fields so any JSON writer can handle it; the gameplay layer writes the
    /// file. Every field has a default, so a file from an older version loads with sensible values
    /// for what it didn't have, and <see cref="SaveMigration"/> fixes anything a default can't.
    /// </summary>
    [Serializable]
    public sealed class CampaignSave
    {
        /// <summary>Bumped whenever the shape changes in a way a default can't cover.</summary>
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;

        /// <summary>The day that was just played. Continue starts the morning after (decision 2026-11-08).</summary>
        public int Day = 1;

        /// <summary>Can be negative: money owed to the boss (decision 2026-11-08).</summary>
        public float Balance;

        /// <summary>Plates broken at the sink and not yet replaced.</summary>
        public int BrokenPlates;

        public List<StationRecord> Stations = new List<StationRecord>();

        /// <summary>Everything lying around the kitchen, flattened: a pan's food points back at the pan.</summary>
        public List<ItemRecord> Items = new List<ItemRecord>();

        public List<StaffRecord> Staff = new List<StaffRecord>();

        public List<ReviewRecord> Reviews = new List<ReviewRecord>();

        /// <summary>Finished days since the last fee.</summary>
        public int DaysTowardsFee;

        /// <summary>Money in and out since the last fee, one amount per ledger line.</summary>
        public float[] Period = new float[0];

        public List<Days.DayRecord> History = new List<Days.DayRecord>();

        // For the slot card in the main menu.
        public string RestaurantName = "";
        public string StyleName = "";
        public string Area = "";

        /// <summary>Real seconds played in this campaign.</summary>
        public float PlayedSeconds;

        /// <summary>When it was last saved, as <see cref="DateTime.Ticks"/> in local time.</summary>
        public long LastPlayedTicks;

        /// <summary>Every player number that has cooked in this campaign, for the card's coloured dots.</summary>
        public List<int> Cooks = new List<int>();

        /// <summary>Review and reply lines used lately, so they don't repeat across days.</summary>
        public List<string> RecentLines = new List<string>();

        /// <summary>Every order ever taken, per dish: not every guest reviews, so reviews can't count them.</summary>
        public List<DishCount> Dishes = new List<DishCount>();
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

        /// <summary>The guest's name as the listing shows it.</summary>
        public string Guest = "";

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

        /// <summary>The owner's mood when they replied (a <see cref="Service.OwnerMood"/>): 0, fresh, for older saves.</summary>
        public int ReplyMood;

        /// <summary>Which visit this was for the guest: 1 (or 0, in older saves) the first.</summary>
        public int Visit;
    }

    /// <summary>Brings a file from an older version up to date, one version at a time.</summary>
    public static class SaveMigration
    {
        /// <returns>False if the file is from a newer version of the game, which can't be read safely.</returns>
        public static bool Upgrade(CampaignSave save)
        {
            if (save == null || save.Version > CampaignSave.CurrentVersion)
                return false;

            // Each step takes version n to n + 1. None yet: version 1 is the first.
            while (save.Version < CampaignSave.CurrentVersion)
                save.Version++;
            return true;
        }
    }
}
