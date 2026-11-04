using System;

namespace Game.Simulation.Staff
{
    public enum StaffRole
    {
        Dishwasher,
        PrepAssistant,
    }

    /// <summary>Tuning for how staff work. Filled from the StaffConfig asset.</summary>
    public sealed class StaffSettings
    {
        /// <summary>In-game seconds to wash a plate at speed 1.</summary>
        public float WashSecondsPerPlate { get; set; } = 150f;

        /// <summary>Fatigue gained per plate washed (0..1 scale).</summary>
        public float FatiguePerPlate { get; set; } = 0.05f;

        /// <summary>Share of their speed lost when fully fatigued.</summary>
        public float FatigueSlowdown { get; set; } = 0.4f;

        /// <summary>Added to the chance of a mistake when fully fatigued.</summary>
        public float FatigueMistakes { get; set; } = 0.1f;

        /// <summary>In-game seconds a prep assistant takes to cut one item at speed 1.</summary>
        public float CutSeconds { get; set; } = 240f;

        /// <summary>Fatigue gained per portion a prep assistant moves or cuts.</summary>
        public float FatiguePerJob { get; set; } = 0.02f;

        /// <summary>Morale someone starts with, and at which it neither helps nor hurts their work.</summary>
        public float NeutralMorale { get; set; } = 0.6f;

        /// <summary>How much morale changes their speed: at 0.3, a full point of morale is 30% of their speed.</summary>
        public float MoraleSpeedEffect { get; set; } = 0.3f;

        /// <summary>Added to the chance of a mistake per point of morale below neutral.</summary>
        public float MoraleMistakeEffect { get; set; } = 0.15f;

        /// <summary>Morale change for the best or worst possible rating of the day; 3 stars is neutral.</summary>
        public float MoraleFromRating { get; set; } = 0.1f;

        /// <summary>Morale lost if everyone walked out, in proportion to the share who did.</summary>
        public float MoraleFromWalkOuts { get; set; } = 0.15f;

        /// <summary>Fatigue at the end of a day above which it starts to hurt morale.</summary>
        public float ComfortableFatigue { get; set; } = 0.5f;

        /// <summary>Morale lost for ending the day completely exhausted.</summary>
        public float MoraleFromOverwork { get; set; } = 0.2f;

        /// <summary>Below this morale at the end of a day, they quit.</summary>
        public float QuitBelow { get; set; } = 0.1f;

        /// <summary>A container below this share of full gets refilled.</summary>
        public float RefillBelow { get; set; } = 0.5f;

        /// <summary>In-game seconds a prep assistant waits for a busy board before giving up.</summary>
        public float BoardPatienceSeconds { get; set; } = 300f;
    }

    /// <summary>Someone who could be hired. Players read the description; the numbers show in how they work.</summary>
    public sealed class Candidate
    {
        public Candidate(string name, StaffRole role, string description, float skill, float speed, float dailyWage)
        {
            Name = name;
            Role = role;
            Description = description;
            Skill = skill;
            Speed = speed;
            DailyWage = dailyWage;
        }

        public string Name { get; }
        public StaffRole Role { get; }
        public string Description { get; }

        /// <summary>0..1: the chance a job goes right when they're fresh.</summary>
        public float Skill { get; }

        /// <summary>1 is an average pace.</summary>
        public float Speed { get; }

        public float DailyWage { get; }
    }

    /// <summary>A person on the team: skilled or not, quick or not, and tiring through a shift.</summary>
    public sealed class StaffMember
    {
        public StaffMember(Candidate hiredFrom)
        {
            Name = hiredFrom.Name;
            Role = hiredFrom.Role;
            Description = hiredFrom.Description;
            Skill = hiredFrom.Skill;
            Speed = hiredFrom.Speed;
            DailyWage = hiredFrom.DailyWage;
        }

        public string Name { get; }
        public StaffRole Role { get; }
        public string Description { get; }
        public float Skill { get; }
        public float Speed { get; }
        public float DailyWage { get; }

        /// <summary>0 (fresh) to 1 (exhausted). Rests overnight.</summary>
        public float Fatigue { get; private set; }

        /// <summary>0 (fed up) to 1 (delighted). Carries from day to day, unlike fatigue.</summary>
        public float Morale { get; set; } = -1f;

        public int JobsToday { get; private set; }
        public int MistakesToday { get; private set; }

        public float WorkingSpeed(StaffSettings settings) =>
            Speed * (1f - settings.FatigueSlowdown * Fatigue) * (1f + settings.MoraleSpeedEffect * (CurrentMorale(settings) - settings.NeutralMorale));

        // Only low morale adds mistakes: being happy doesn't make someone more skilled.
        public float MistakeChance(StaffSettings settings) =>
            Math.Clamp(1f - Skill + settings.FatigueMistakes * Fatigue + settings.MoraleMistakeEffect * Math.Max(0f, settings.NeutralMorale - CurrentMorale(settings)), 0f, 1f);

        // Someone new starts at neutral.
        private float CurrentMorale(StaffSettings settings)
        {
            if (Morale < 0f)
                Morale = settings.NeutralMorale;
            return Morale;
        }

        /// <summary>
        /// The day is over: morale follows how the service went and how hard they worked. A good
        /// rating lifts everyone, walk-outs get them down, and ending a day exhausted wears on them.
        /// </summary>
        public void EndDay(float? rating, float walkOutShare, StaffSettings settings)
        {
            var morale = CurrentMorale(settings);
            if (rating.HasValue)
                morale += settings.MoraleFromRating * Math.Clamp((rating.Value - 3f) / 2f, -1f, 1f);
            morale -= settings.MoraleFromWalkOuts * Math.Clamp(walkOutShare, 0f, 1f);
            if (settings.ComfortableFatigue < 1f && Fatigue > settings.ComfortableFatigue)
                morale -= settings.MoraleFromOverwork * (Fatigue - settings.ComfortableFatigue) / (1f - settings.ComfortableFatigue);
            Morale = Math.Clamp(morale, 0f, 1f);
        }

        public void RecordJob(float fatigue, bool mistake)
        {
            Fatigue = Math.Clamp(Fatigue + fatigue, 0f, 1f);
            JobsToday++;
            if (mistake)
                MistakesToday++;
        }

        /// <summary>A new day: rested, with a clean slate.</summary>
        public void StartDay()
        {
            Fatigue = 0f;
            JobsToday = 0;
            MistakesToday = 0;
        }
    }
}
