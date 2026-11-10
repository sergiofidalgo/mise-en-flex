using Game.Simulation.Days;
using Game.Simulation.Staff;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// forkcast in Plan (the user's calls, 2026-11-10): when the dashboard is up, and the Staff tab's
    /// hiring rules.
    /// </summary>
    public class ForkcastPlanTests
    {
        // ---- When it's up ----

        [TestCase(DayPhase.Plan, true)]
        [TestCase(DayPhase.Prep, false)]
        [TestCase(DayPhase.Service, false)]
        [TestCase(DayPhase.Review, true)]
        public void A_kitchen_loading_into_a_phase_opens_on_it_only_for_plan_and_review(DayPhase phase, bool open)
        {
            Assert.That(new ForkcastScreen(phase).IsOpen, Is.EqualTo(open));
        }

        [Test]
        public void It_stays_up_from_review_into_the_next_morning()
        {
            var screen = new ForkcastScreen(DayPhase.Review);
            var changes = 0;
            screen.Changed += _ => changes++;
            screen.PhaseChanged(DayPhase.Plan);
            Assert.That(screen.IsOpen, Is.True);
            Assert.That(changes, Is.Zero, "No flicker as the day changes.");
        }

        [Test]
        public void It_goes_away_for_prep_and_service_and_comes_back_for_review()
        {
            var screen = new ForkcastScreen(DayPhase.Plan);
            screen.PhaseChanged(DayPhase.Prep);
            Assert.That(screen.IsOpen, Is.False);
            screen.PhaseChanged(DayPhase.Service);
            Assert.That(screen.IsOpen, Is.False);
            screen.PhaseChanged(DayPhase.Review);
            Assert.That(screen.IsOpen, Is.True);
        }

        [TestCase(DayPhase.Review, 2)]
        [TestCase(DayPhase.Plan, 1)]
        [TestCase(DayPhase.Prep, 0)]
        [TestCase(DayPhase.Service, 0)]
        public void Start_prep_goes_from_review_through_the_morning_in_one_hold(DayPhase phase, int steps)
        {
            Assert.That(ForkcastScreen.StepsToPrep(phase), Is.EqualTo(steps));
        }

        [Test]
        public void One_hold_from_review_lands_in_prep_with_it_closed()
        {
            var days = new DayCycle(100f);
            var screen = new ForkcastScreen(days.Phase);
            days.PhaseChanged += screen.PhaseChanged;
            days.Advance();
            days.Advance();
            days.FinishService(Game.Simulation.Service.ServiceSummary.From(new Game.Simulation.Service.ServiceSimulation(new Game.Simulation.Service.ServiceSettings(), seatCount: 1)));
            Assert.That(screen.IsOpen, Is.True, "up for the Review");
            var opened = 0;
            screen.Changed += open => opened += open ? 1 : -1;
            var steps = ForkcastScreen.StepsToPrep(days.Phase);
            for (var i = 0; i < steps; i++)
                days.Advance();
            Assert.That(days.Phase, Is.EqualTo(DayPhase.Prep));
            Assert.That(days.Day, Is.EqualTo(2));
            Assert.That(screen.IsOpen, Is.False);
            Assert.That(opened, Is.EqualTo(-1), "it closes once, with no flicker through the morning");
        }

        // ---- The Staff tab ----

        private static Candidate Washer(string name = "Ana", float wage = 40f) => new Candidate(name, StaffRole.Dishwasher, "steady", 0.8f, 1f, wage);
        private static Candidate Helper(string name = "Omar", float wage = 80f) => new Candidate(name, StaffRole.PrepAssistant, "trained", 0.9f, 1f, wage);

        private static StaffRoster Roster(params Candidate[] candidates) => new StaffRoster { Candidates = candidates };

        [Test]
        public void The_list_is_the_team_then_todays_candidates()
        {
            var roster = Roster(Washer(), Helper());
            roster.Restore(new StaffMember(Washer("Lucía")));
            var rows = StaffBoard.Rows(roster);
            Assert.That(rows.Count, Is.EqualTo(3));
            Assert.That(rows[0].IsCandidate, Is.False);
            Assert.That(rows[0].Member.Name, Is.EqualTo("Lucía"));
            Assert.That(rows[1].Candidate.Name, Is.EqualTo("Ana"));
            Assert.That(rows[2].Candidate.Name, Is.EqualTo("Omar"));
        }

        [Test]
        public void A_hires_the_chosen_candidate_who_then_leaves_the_candidates()
        {
            var roster = Roster(Washer(), Helper());
            Assert.That(StaffBoard.Hire(roster, 1, 500f, editable: true), Is.EqualTo(StaffBoard.Outcome.Hired));
            Assert.That(roster.Hired.Count, Is.EqualTo(1));
            Assert.That(roster.Hired[0].Name, Is.EqualTo("Omar"));
            var rows = StaffBoard.Rows(roster);
            Assert.That(rows.Count, Is.EqualTo(2), "Omar is listed once, on the team.");
            Assert.That(rows[0].Member.Name, Is.EqualTo("Omar"));
            Assert.That(rows[1].Candidate.Name, Is.EqualTo("Ana"));
        }

        [Test]
        public void A_refuses_a_candidate_out_of_reach_and_says_why()
        {
            var roster = Roster(Helper(wage: 80f));
            Assert.That(StaffBoard.Hire(roster, 0, 79f, editable: true), Is.EqualTo(StaffBoard.Outcome.CantAfford));
            Assert.That(roster.Hired, Is.Empty);
        }

        [Test]
        public void A_refuses_a_second_person_for_a_role_already_filled()
        {
            var roster = Roster(Washer("Ana"), Washer("Bea"));
            StaffBoard.Hire(roster, 0, 500f, editable: true);
            // Now: Ana on the team, Bea a candidate for the same job.
            Assert.That(StaffBoard.WhyNot(roster, roster.Candidates[1], 500f), Is.EqualTo(StaffBoard.Outcome.RoleTaken));
            Assert.That(StaffBoard.Hire(roster, 1, 500f, editable: true), Is.EqualTo(StaffBoard.Outcome.RoleTaken));
            Assert.That(roster.Hired.Count, Is.EqualTo(1));
        }

        [Test]
        public void Wages_already_owed_count_against_the_balance()
        {
            var roster = Roster(Washer(wage: 40f), Helper(wage: 80f));
            StaffBoard.Hire(roster, 0, 100f, editable: true);
            Assert.That(StaffBoard.WhyNot(roster, roster.Candidates[1], 100f), Is.EqualTo(StaffBoard.Outcome.CantAfford));
            Assert.That(StaffBoard.WhyNot(roster, roster.Candidates[1], 120f), Is.EqualTo(StaffBoard.Outcome.Nothing));
        }

        [Test]
        public void A_on_a_team_member_and_Y_on_a_candidate_do_nothing()
        {
            var roster = Roster(Washer("Ana"), Helper());
            roster.Restore(new StaffMember(Washer("Lucía")));
            Assert.That(StaffBoard.Hire(roster, 0, 500f, editable: true), Is.EqualTo(StaffBoard.Outcome.Nothing));
            Assert.That(StaffBoard.LetGo(roster, 2, editable: true), Is.EqualTo(StaffBoard.Outcome.Nothing));
            Assert.That(roster.Hired.Count, Is.EqualTo(1));
        }

        [Test]
        public void Y_lets_the_chosen_team_member_go()
        {
            var roster = Roster(Helper());
            roster.Restore(new StaffMember(Washer("Lucía")));
            roster.Restore(new StaffMember(Washer("Bea")));
            Assert.That(StaffBoard.LetGo(roster, 0, editable: true), Is.EqualTo(StaffBoard.Outcome.LetGo));
            Assert.That(roster.Hired.Count, Is.EqualTo(1));
            Assert.That(roster.Hired[0].Name, Is.EqualTo("Bea"));
        }

        [Test]
        public void A_read_only_tab_changes_nothing()
        {
            var roster = Roster(Helper());
            roster.Restore(new StaffMember(Washer("Lucía")));
            Assert.That(StaffBoard.Hire(roster, 1, 500f, editable: false), Is.EqualTo(StaffBoard.Outcome.ReadOnly));
            Assert.That(StaffBoard.LetGo(roster, 0, editable: false), Is.EqualTo(StaffBoard.Outcome.ReadOnly));
            Assert.That(roster.Hired.Count, Is.EqualTo(1));
        }

        [Test]
        public void The_choice_stays_inside_the_list()
        {
            Assert.That(StaffBoard.Step(0, -1, 3), Is.EqualTo(0));
            Assert.That(StaffBoard.Step(2, 1, 3), Is.EqualTo(2));
            Assert.That(StaffBoard.Step(1, 1, 3), Is.EqualTo(2));
            Assert.That(StaffBoard.Step(4, 0, 0), Is.EqualTo(0));
        }
    }
}
