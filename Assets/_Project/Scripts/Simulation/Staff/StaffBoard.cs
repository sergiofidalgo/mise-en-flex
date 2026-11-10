using System.Collections.Generic;

namespace Game.Simulation.Staff
{
    /// <summary>
    /// The forkcast Staff tab's rules (the user's call, 2026-11-10): one list, the team first and
    /// then today's candidates, browsed with the D-pad. A hires the chosen candidate, Y lets the
    /// chosen team member go. Anyone can, but only in Plan: in Review the tab is read-only.
    /// </summary>
    public static class StaffBoard
    {
        public enum Outcome
        {
            Nothing,
            Hired,
            LetGo,
            CantAfford,
            RoleTaken,
            ReadOnly,
        }

        /// <summary>A row: a team member or a candidate, never both.</summary>
        public readonly struct Row
        {
            public Row(StaffMember member, Candidate candidate)
            {
                Member = member;
                Candidate = candidate;
            }

            public StaffMember Member { get; }
            public Candidate Candidate { get; }
            public bool IsCandidate => Candidate != null;
        }

        public static List<Row> Rows(StaffRoster roster)
        {
            var rows = new List<Row>();
            foreach (var member in roster.Hired)
                rows.Add(new Row(member, null));
            // Someone hired today is on the team now, not still looking for work.
            foreach (var candidate in roster.Candidates)
                if (!IsOnTeam(roster, candidate))
                    rows.Add(new Row(null, candidate));
            return rows;
        }

        private static bool IsOnTeam(StaffRoster roster, Candidate candidate)
        {
            foreach (var member in roster.Hired)
                if (member.Name == candidate.Name && member.Role == candidate.Role)
                    return true;
            return false;
        }

        /// <summary>The chosen row after a step, held inside the list (no wrapping, like the other tabs).</summary>
        public static int Step(int selected, int step, int count) =>
            count <= 0 ? 0 : System.Math.Clamp(selected + step, 0, count - 1);

        /// <summary>Why a candidate can't be hired, or Nothing when they can.</summary>
        public static Outcome WhyNot(StaffRoster roster, Candidate candidate, float balance)
        {
            if (roster.HasRole(candidate.Role))
                return Outcome.RoleTaken;
            if (!roster.CanHire(candidate, balance))
                return Outcome.CantAfford;
            return Outcome.Nothing;
        }

        /// <summary>A on the chosen row.</summary>
        public static Outcome Hire(StaffRoster roster, int selected, float balance, bool editable)
        {
            var rows = Rows(roster);
            if (selected < 0 || selected >= rows.Count || !rows[selected].IsCandidate)
                return Outcome.Nothing;
            if (!editable)
                return Outcome.ReadOnly;
            var why = WhyNot(roster, rows[selected].Candidate, balance);
            if (why != Outcome.Nothing)
                return why;
            roster.Hire(rows[selected].Candidate, balance);
            return Outcome.Hired;
        }

        /// <summary>Y on the chosen row.</summary>
        public static Outcome LetGo(StaffRoster roster, int selected, bool editable)
        {
            var rows = Rows(roster);
            if (selected < 0 || selected >= rows.Count || rows[selected].IsCandidate)
                return Outcome.Nothing;
            if (!editable)
                return Outcome.ReadOnly;
            roster.Fire(rows[selected].Member);
            return Outcome.LetGo;
        }
    }
}
