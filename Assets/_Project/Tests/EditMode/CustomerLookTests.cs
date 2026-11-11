using System.Linq;
using Game.Data;
using Game.Presentation;
using Game.Simulation.Saving;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Each customer wears one of the 3D looks, their review keeps it, and a returning guest comes
    /// back in the same one, so the review card's portrait is the person who came (2026-11-11).
    /// </summary>
    public class CustomerLookTests
    {
        private static ServiceSimulation Service(int looks, int seed = 3) => new ServiceSimulation(new ServiceSettings
        {
            DurationSeconds = 6f * 3600f,
            ArrivalIntervalSeconds = 10f * 60f,
            ArrivalJitter = 0f,
            PatienceSeconds = 24f * 3600f,
            CustomerLooks = looks,
            Seed = seed,
        }, seatCount: 40);

        [Test]
        public void EveryCustomerWearsOneOfTheLooks()
        {
            var service = Service(6);
            service.Tick(4f * 3600f);
            Assert.That(service.Customers.Count, Is.GreaterThan(10));
            Assert.That(service.Customers.All(c => c.Look >= 0 && c.Look < 6));
            Assert.That(service.Customers.Select(c => c.Look).Distinct().Count(), Is.GreaterThan(1), "not everyone looks the same");
        }

        [Test]
        public void TheSameDayBringsTheSameLooksAndNoLooksMeansNone()
        {
            var first = Service(6);
            var again = Service(6);
            first.Tick(4f * 3600f);
            again.Tick(4f * 3600f);
            Assert.That(again.Customers.Select(c => c.Look), Is.EqualTo(first.Customers.Select(c => c.Look)));

            var none = Service(0);
            none.Tick(2f * 3600f);
            Assert.That(none.Customers.All(c => c.Look == -1));
        }

        [Test]
        public void LooksDontChangeWhatAnyoneOrders()
        {
            var with = Service(6);
            var without = Service(0);
            with.Tick(4f * 3600f);
            without.Tick(4f * 3600f);
            Assert.That(with.Customers.Select(c => (c.ArrivedAt, c.Party.Count)), Is.EqualTo(without.Customers.Select(c => (c.ArrivedAt, c.Party.Count))),
                "the looks roll their own dice: arrivals and parties are as before");
        }

        [Test]
        public void OlderReviewsHaveNoLook()
        {
            var record = JsonUtility.FromJson<ReviewRecord>("{\"Guest\":\"Mike S.\",\"Day\":2}");
            Assert.That(record.Look, Is.EqualTo(-1));
        }

        [Test]
        public void AReturningGuestComesBackInTheirOwnLook()
        {
            var book = new GuestBook { ReturnShare = 1f };
            var history = new[]
            {
                new ReviewRecord { Guest = "Ann B.", Day = 1, Look = 2, Topic = (int)ReviewTopic.Praise },
                new ReviewRecord { Guest = "Bob C.", Day = 1, Look = 4, Topic = (int)ReviewTopic.Praise },
                new ReviewRecord { Guest = "Old D.", Day = 1, Look = -1, Topic = (int)ReviewTopic.Praise },
            };
            for (var seed = 0; seed < 40; seed++)
            {
                book.Rebuild(history, today: 2);
                var guest = book.Draw(new System.Random(seed), look: 4);
                Assert.That(guest == null || guest.Name == "Bob C." || guest.Name == "Old D.", $"seed {seed} drew {guest?.Name}");
            }
            book.Rebuild(history, today: 2);
            Assert.That(book.Draw(new System.Random(1), look: 5)?.Name, Is.Null.Or.EqualTo("Old D."), "nobody else wore look 5");
        }

        [Test]
        public void TheConfigCountsTheCustomerPrefabsLooks()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PF_Customer.prefab");
            var config = AssetDatabase.FindAssets("t:ServiceConfig").Select(g => AssetDatabase.LoadAssetAtPath<ServiceConfig>(AssetDatabase.GUIDToAssetPath(g))).First();
            Assert.That(prefab, Is.Not.Null);
            Assert.That(config.ToSettings().CustomerLooks, Is.EqualTo(prefab.GetComponent<CharacterLookPicker>().Count),
                "ServiceConfig's customer looks must match PF_Customer's looks, or cards show the wrong face");
        }
    }
}
