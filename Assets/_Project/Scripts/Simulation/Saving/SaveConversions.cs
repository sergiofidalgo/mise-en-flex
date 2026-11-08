using Game.Simulation.Service;
using Game.Simulation.Staff;

namespace Game.Simulation.Saving
{
    /// <summary>Between simulation objects and their save records.</summary>
    public static class SaveConversions
    {
        public static StaffRecord ToRecord(StaffMember member) => new StaffRecord
        {
            Name = member.Name,
            Role = (int)member.Role,
            Description = member.Description,
            Skill = member.Skill,
            Speed = member.Speed,
            DailyWage = member.DailyWage,
            Morale = member.Morale,
        };

        /// <summary>Back on the team as they were; fatigue rests overnight, so it starts fresh.</summary>
        public static StaffMember ToMember(StaffRecord record) =>
            new StaffMember(new Candidate(record.Name, (StaffRole)record.Role, record.Description, record.Skill, record.Speed, record.DailyWage))
            {
                Morale = record.Morale,
            };

        public static ReviewRecord ToRecord(Review review, int day, string guest = "")
        {
            var customer = review.Customer;
            var order = customer?.Order;
            return new ReviewRecord
            {
                Day = day,
                Stars = review.Stars,
                Topic = (int)review.Topic,
                Line = review.Line ?? "",
                Guest = guest ?? "",
                Order = order == null ? "" : $"{order.MainName.ToLowerInvariant()}, {order.Doneness.Name.ToLowerInvariant()}",
                ArrivedAt = customer?.ArrivedAt ?? 0f,
                WalkedOut = customer?.WalkedOut ?? false,
                Paid = customer?.Bill?.Payment ?? 0f,
                Tip = customer?.Bill?.Tip ?? 0f,
                Penalty = customer?.WalkOutPenalty ?? 0f,
            };
        }
    }
}
