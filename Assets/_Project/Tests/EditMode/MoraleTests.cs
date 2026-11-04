using System;
using Game.Data;
using Game.Simulation.Service;
using Game.Simulation.Staff;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    public class MoraleTests
    {
        private readonly StaffSettings _settings = new StaffSettings();

        private static StaffMember Person(float skill = 0.9f) =>
            new StaffMember(new Candidate("Ana", StaffRole.Dishwasher, "steady", skill, 1f, 40f));

        private StaffMember PersonWithMorale(float morale, float skill = 0.9f)
        {
            var person = Person(skill);
            person.Morale = morale;
            return person;
        }

        [Test]
        public void SomeoneNewWorksAtTheirUsualPace()
        {
            var person = Person();
            Assert.That(person.WorkingSpeed(_settings), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(person.MistakeChance(_settings), Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void HappyPeopleWorkFasterAndUnhappyOnesSlower()
        {
            Assert.That(PersonWithMorale(1f).WorkingSpeed(_settings), Is.GreaterThan(1f));
            Assert.That(PersonWithMorale(0.2f).WorkingSpeed(_settings), Is.LessThan(1f));
        }

        [Test]
        public void OnlyLowMoraleAddsMistakes()
        {
            Assert.That(PersonWithMorale(1f).MistakeChance(_settings), Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(PersonWithMorale(0.2f).MistakeChance(_settings), Is.GreaterThan(0.1f));
        }

        [Test]
        public void AGoodDayLiftsMoraleAndABadOneLowersIt()
        {
            var good = PersonWithMorale(0.5f);
            good.EndDay(5f, 0f, _settings);
            Assert.That(good.Morale, Is.EqualTo(0.6f).Within(0.0001f));

            var bad = PersonWithMorale(0.5f);
            bad.EndDay(1f, 0f, _settings);
            Assert.That(bad.Morale, Is.EqualTo(0.4f).Within(0.0001f));

            var neutral = PersonWithMorale(0.5f);
            neutral.EndDay(3f, 0f, _settings);
            Assert.That(neutral.Morale, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void WalkOutsGetThemDown()
        {
            var person = PersonWithMorale(0.5f);
            person.EndDay(3f, 0.5f, _settings);
            Assert.That(person.Morale, Is.EqualTo(0.425f).Within(0.0001f));
        }

        [Test]
        public void OverworkWearsOnThem()
        {
            var rested = PersonWithMorale(0.5f);
            rested.RecordJob(0.4f, false);
            rested.EndDay(3f, 0f, _settings);
            Assert.That(rested.Morale, Is.EqualTo(0.5f).Within(0.0001f), "a normal day's work is fine");

            var tired = PersonWithMorale(0.5f);
            tired.RecordJob(1f, false);
            tired.EndDay(3f, 0f, _settings);
            Assert.That(tired.Morale, Is.EqualTo(0.3f).Within(0.0001f));
        }

        [Test]
        public void MoraleStaysInRange()
        {
            var high = PersonWithMorale(0.99f);
            high.EndDay(5f, 0f, _settings);
            Assert.That(high.Morale, Is.EqualTo(1f));

            var low = PersonWithMorale(0.01f);
            low.EndDay(1f, 1f, _settings);
            Assert.That(low.Morale, Is.EqualTo(0f));
        }

        [Test]
        public void ANightsRestFixesFatigueButNotMorale()
        {
            var person = PersonWithMorale(0.3f);
            person.RecordJob(0.5f, false);
            person.StartDay();
            Assert.That(person.Fatigue, Is.EqualTo(0f));
            Assert.That(person.Morale, Is.EqualTo(0.3f));
        }

        [Test]
        public void TheFedUpQuit()
        {
            var roster = new StaffRoster();
            var person = roster.Hire(new Candidate("Ana", StaffRole.Dishwasher, "steady", 1f, 1f, 10f), 100f);
            person.Morale = 0.05f;

            // No reviews, nobody walked out: nothing lifts them.
            roster.EndDay(EmptyDay(), _settings);

            Assert.That(roster.Hired, Is.Empty);
            Assert.That(roster.LastQuit, Is.EqualTo(new[] { person }));
            Assert.That(roster.TotalWages, Is.EqualTo(0f));

            roster.EndDay(EmptyDay(), _settings);
            Assert.That(roster.LastQuit, Is.Empty, "only the latest day");
        }

        [Test]
        public void ContentStaffStay()
        {
            var roster = new StaffRoster();
            roster.Hire(new Candidate("Ana", StaffRole.Dishwasher, "steady", 1f, 1f, 10f), 100f);
            roster.EndDay(EmptyDay(), _settings);
            Assert.That(roster.Hired, Has.Count.EqualTo(1));
            Assert.That(roster.LastQuit, Is.Empty);
        }

        [Test]
        public void IfTheyStartNeutralOneBadDayIsntEnoughToQuit()
        {
            var person = Person();
            person.RecordJob(1f, false);
            person.EndDay(1f, 1f, _settings);
            Assert.That(person.Morale, Is.GreaterThanOrEqualTo(_settings.QuitBelow));
        }

        [Test]
        public void TheConfigFillsTheMoraleSettings()
        {
            var config = AssetDatabase.LoadAssetAtPath<StaffConfig>("Assets/_Project/Data/Settings/StaffConfig.asset");
            var settings = config.ToSettings();
            Assert.That(settings.NeutralMorale, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(settings.QuitBelow, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(settings.MoraleFromRating, Is.GreaterThan(0f));
        }

        private static ServiceSummary EmptyDay() =>
            ServiceSummary.From(new ServiceSimulation(new ServiceSettings(), seatCount: 1));
    }
}
