using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Simulation.Staff
{
    /// <summary>
    /// Who works here and who could. Hiring rules only: the Plan screen is just one way to reach
    /// them (see the errands idea in docs/ideas.md). Fixed roles, one person per role for now.
    /// </summary>
    public sealed class StaffRoster
    {
        private readonly List<StaffMember> _hired = new List<StaffMember>();

        public IReadOnlyList<StaffMember> Hired => _hired;

        /// <summary>Today's candidates.</summary>
        public IReadOnlyList<Candidate> Candidates { get; set; } = Array.Empty<Candidate>();

        public float TotalWages => _hired.Sum(m => m.DailyWage);

        public bool HasRole(StaffRole role) => _hired.Any(m => m.Role == role);

        /// <summary>You can't hire what you can't afford: real debt waits for Phase 5.</summary>
        public bool CanHire(Candidate candidate, float balance) =>
            candidate != null && !HasRole(candidate.Role) && balance >= TotalWages + candidate.DailyWage;

        public StaffMember Hire(Candidate candidate, float balance)
        {
            if (!CanHire(candidate, balance))
                throw new InvalidOperationException($"Can't hire {candidate?.Name}.");
            var member = new StaffMember(candidate);
            _hired.Add(member);
            return member;
        }

        public void Fire(StaffMember member) => _hired.Remove(member);
    }

    /// <summary>
    /// Who's looking for work each day: a few people drawn from the templates, the same ones for
    /// the same day number.
    /// </summary>
    public static class StaffMarket
    {
        public static IReadOnlyList<Candidate> CandidatesFor(int day, int seed, int count, IReadOnlyList<string> names, IReadOnlyList<Candidate> templates)
        {
            var random = new Random(unchecked(seed * 31 + day));
            var shuffledNames = names.OrderBy(_ => random.Next()).ToList();
            return templates
                .OrderBy(_ => random.Next())
                .Take(count)
                .Select((t, i) => new Candidate(shuffledNames.Count > 0 ? shuffledNames[i % shuffledNames.Count] : t.Name, t.Role, t.Description, t.Skill, t.Speed, t.DailyWage))
                .ToList();
        }
    }
}
