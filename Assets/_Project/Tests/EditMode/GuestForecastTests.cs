using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>The guests expected tonight, shown during Prep (2026-11-10).</summary>
    public class GuestForecastTests
    {
        private static ServiceSettings Settings() => new ServiceSettings
        {
            DurationSeconds = 100f,
            LastOrdersSeconds = 0f,
            ArrivalIntervalSeconds = 10f,
            RushStart = 0.4f,
            RushEnd = 0.6f,
            RushMultiplier = 2f,
        };

        [Test]
        public void Groups_follow_the_arrival_rule_with_the_rush()
        {
            // 80 calm seconds at one per 10, 20 rush seconds at one per 5.
            Assert.That(GuestForecast.Groups(Settings(), 1f), Is.EqualTo(12f).Within(0.001f));
        }

        [Test]
        public void Nobody_arrives_after_last_orders()
        {
            var settings = Settings();
            settings.LastOrdersSeconds = 50f; // open 50 s: 40 calm, rush 40–50 s
            Assert.That(GuestForecast.Groups(settings, 1f), Is.EqualTo(4f + 2f).Within(0.001f));
        }

        [Test]
        public void Dearer_prices_mean_fewer_groups()
        {
            Assert.That(GuestForecast.Groups(Settings(), 0.5f), Is.EqualTo(6f).Within(0.001f));
            Assert.That(GuestForecast.Groups(Settings(), 2f), Is.EqualTo(24f).Within(0.001f));
        }

        [Test]
        public void Stools_bring_people_alone_and_tables_bring_groups()
        {
            Assert.That(GuestForecast.GroupSize(new[] { 1, 1, 1 }), Is.EqualTo(1f));
            Assert.That(GuestForecast.GroupSize(new int[0]), Is.EqualTo(1f));
            // One table for four: groups of 1–4 equally likely, 2.5 on average.
            Assert.That(GuestForecast.GroupSize(new[] { 4 }), Is.EqualTo(2.5f).Within(0.001f));
        }

        [Test]
        public void The_average_group_matches_what_the_service_rolls()
        {
            var tables = new[] { 1, 1, 1, 4 };
            var random = new Random(7);
            var rolled = Enumerable.Range(0, 20000).Average(_ => PartyMix.PickSize(tables, random));
            Assert.That(GuestForecast.GroupSize(tables), Is.EqualTo((float)rolled).Within(0.03f));
        }

        [Test]
        public void Guests_are_groups_times_their_size_rounded()
        {
            Assert.That(GuestForecast.Guests(Settings(), 1f, new[] { 4 }), Is.EqualTo(30));
            Assert.That(GuestForecast.Guests(Settings(), 1f, new List<int>()), Is.EqualTo(12));
        }
    }
}
