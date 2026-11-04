using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Items;
using Game.Simulation.Quality;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class ServiceSimulationTests
    {
        private const float Minute = 60f;
        private const float Hour = 3600f;

        // Patience defaults to longer than a service, so seating tests aren't cut short by walk-outs.
        private static ServiceSettings Settings(float interval = 12f * Minute, float jitter = 0f, float rush = 1f, int queue = 3,
            float patience = 24f * Hour, int seed = 7) => new ServiceSettings
        {
            PatienceSeconds = patience,
            QueuePatienceRate = 0.5f,
            DurationSeconds = 6f * Hour,
            ArrivalIntervalSeconds = interval,
            ArrivalJitter = jitter,
            RushStart = 0.35f,
            RushEnd = 0.6f,
            RushMultiplier = rush,
            EatSeconds = 15f * Minute,
            MaxQueue = queue,
            Seed = seed,
        };

        private static Plate PlateWithFood()
        {
            var plate = new Plate(3);
            plate.Put(new Item("Steak"));
            return plate;
        }

        // Ticks in one-minute steps, like a frame at 60x, and sits everyone down who reached their seat.
        private static void Run(ServiceSimulation service, float seconds)
        {
            for (var t = 0f; t < seconds; t += Minute)
            {
                service.Tick(Minute);
                foreach (var c in service.Customers.Where(c => c.State == CustomerState.WalkingToSeat))
                    service.SitDown(c);
            }
        }

        [Test]
        public void CustomersArriveAtTheConfiguredRate()
        {
            var service = new ServiceSimulation(Settings(interval: 12f * Minute), seatCount: 30);
            service.Tick(2f * Hour);
            Assert.That(service.Customers.Count, Is.EqualTo(10));
        }

        [Test]
        public void TheRushBringsCustomersFaster()
        {
            var calm = new ServiceSimulation(Settings(rush: 1f), seatCount: 99);
            var rush = new ServiceSimulation(Settings(rush: 2.5f), seatCount: 99);
            calm.Tick(6f * Hour);
            rush.Tick(6f * Hour);
            Assert.That(rush.Customers.Count, Is.GreaterThan(calm.Customers.Count + 5));
        }

        [Test]
        public void NobodyArrivesAfterClosing()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 99);
            service.Tick(6f * Hour);
            var count = service.Customers.Count;
            service.Tick(2f * Hour);
            Assert.That(service.IsClosed);
            Assert.That(service.Customers.Count, Is.EqualTo(count));
        }

        [Test]
        public void ArrivalsTakeAFreeSeat()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 3);
            service.Tick(13f * Minute);
            var first = service.Customers.Single();
            Assert.That(first.State, Is.EqualTo(CustomerState.WalkingToSeat));
            Assert.That(service.AtSeat(first.Seat), Is.SameAs(first));
        }

        [Test]
        public void WhenTheStoolsAreFullTheyQueueThenWalkAway()
        {
            var service = new ServiceSimulation(Settings(queue: 2), seatCount: 1);
            Run(service, 4f * 12f * Minute + Minute); // four arrivals, nobody served

            Assert.That(service.Queue, Has.Count.EqualTo(2));
            Assert.That(service.LostCovers, Is.EqualTo(1));
        }

        [Test]
        public void OnlyASeatedWaitingCustomerCanBeServed()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 3);
            service.Tick(13f * Minute);
            var customer = service.Customers.Single();

            Assert.That(service.Serve(customer.Seat, PlateWithFood()), Is.False, "still walking to the stool");
            service.SitDown(customer);
            Assert.That(service.Serve(customer.Seat, new Plate(3)), Is.False, "an empty plate isn't a meal");
            Assert.That(service.Serve(customer.Seat, PlateWithFood()), Is.True);
            Assert.That(customer.State, Is.EqualTo(CustomerState.Eating));
        }

        [Test]
        public void ServedCustomersKeepTheirDishScoreAsSatisfaction()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 3);
            Assert.That(service.AverageStars, Is.Null);
            Run(service, 2f * 12f * Minute + Minute); // two seated customers
            var first = service.AtSeat(0);
            var second = service.AtSeat(1);

            var great = new DishScore(System.Array.Empty<ItemScore>(), 0.9f);
            var poor = new DishScore(System.Array.Empty<ItemScore>(), 0.4f);
            service.Serve(0, PlateWithFood(), great);
            service.Serve(1, PlateWithFood(), poor);

            Assert.That(first.Satisfaction, Is.SameAs(great));
            Assert.That(second.Satisfaction, Is.SameAs(poor));
            Assert.That(service.AverageStars, Is.EqualTo((4.5f + 2f) / 2f));
        }

        [Test]
        public void SeatedCustomersWalkOutWhenPatienceRunsOut()
        {
            var service = new ServiceSimulation(Settings(interval: 12f * Minute, patience: 30f * Minute), seatCount: 3);
            Run(service, 13f * Minute);
            var customer = service.AtSeat(0);
            Assert.That(customer.State, Is.EqualTo(CustomerState.Waiting));

            Run(service, 15f * Minute);
            Assert.That(customer.Patience, Is.EqualTo(0.5f).Within(0.05f));
            Run(service, 16f * Minute);

            Assert.That(customer.State, Is.EqualTo(CustomerState.Leaving));
            Assert.That(customer.WalkedOut && customer.RanOutOfPatience);
            Assert.That(service.AtSeat(0), Is.Not.SameAs(customer), "their stool is free again");
            Assert.That(service.WalkOuts, Is.EqualTo(1));
            Assert.That(service.LostCovers, Is.EqualTo(1));
        }

        [Test]
        public void ServedCustomersStopLosingPatience()
        {
            var service = new ServiceSimulation(Settings(patience: 30f * Minute), seatCount: 3);
            Run(service, 13f * Minute);
            var customer = service.AtSeat(0);
            Run(service, 10f * Minute);
            service.Serve(0, PlateWithFood());
            var whenServed = customer.Patience;

            service.Tick(10f * Minute);

            Assert.That(customer.PatienceWhenServed, Is.EqualTo(whenServed));
            Assert.That(customer.Patience, Is.EqualTo(whenServed));
            Assert.That(customer.RanOutOfPatience, Is.False);
        }

        [Test]
        public void QueueingDrainsPatienceMoreSlowly()
        {
            var service = new ServiceSimulation(Settings(patience: 60f * Minute), seatCount: 1);
            Run(service, 2f * 12f * Minute + Minute); // first seated at ~12, second queueing from ~24
            var seated = service.AtSeat(0);
            var queued = service.Queue.Single();
            Run(service, 20f * Minute);

            var seatedLost = 1f - seated.Patience;
            var queuedLost = 1f - queued.Patience;
            Assert.That(queuedLost, Is.LessThan(seatedLost));
            Assert.That(queuedLost / (20f / 60f), Is.EqualTo(0.5f).Within(0.08f), "half the seated rate");
        }

        [Test]
        public void ImpatientQueuersLeaveTheQueue()
        {
            // No stools: everyone queues. 10 min of patience at half rate lasts 20 min by the door.
            var service = new ServiceSimulation(Settings(interval: 12f * Minute, patience: 10f * Minute), seatCount: 0);
            Run(service, 13f * Minute);
            var queued = service.Queue.Single();
            Run(service, 21f * Minute);

            Assert.That(queued.RanOutOfPatience);
            Assert.That(queued.State, Is.EqualTo(CustomerState.Leaving));
            Assert.That(service.Queue.Contains(queued), Is.False);
        }

        [Test]
        public void PatienceDialScalesTheWaitAndZeroMeansForever()
        {
            var patient = new ServiceSimulation(Settings(patience: 30f * Minute), seatCount: 3, patienceMultiplier: 2f);
            var saint = new ServiceSimulation(Settings(patience: 30f * Minute), seatCount: 3, patienceMultiplier: 0f);
            Run(patient, 13f * Minute + 40f * Minute);
            Run(saint, 13f * Minute + 5f * Hour);

            Assert.That(patient.WalkOuts, Is.EqualTo(0), "60 min of patience at ×2");
            Assert.That(patient.AtSeat(0).Patience, Is.EqualTo(1f - 40f / 60f).Within(0.05f));
            Assert.That(saint.WalkOuts, Is.EqualTo(0));
        }

        [Test]
        public void TheDoorClosesAtLastOrders()
        {
            var settings = Settings(interval: 10f * Minute);
            settings.LastOrdersSeconds = 1f * Hour;
            var service = new ServiceSimulation(settings, seatCount: 99);

            service.Tick(5f * Hour - Minute);
            Assert.That(service.Phase, Is.EqualTo(ServicePhase.Open));
            service.Tick(2f * Minute);
            Assert.That(service.Phase, Is.EqualTo(ServicePhase.LastOrders));

            var count = service.Customers.Count;
            service.Tick(30f * Minute);
            Assert.That(service.Customers.Count, Is.EqualTo(count));
        }

        [Test]
        public void TheServiceFinishesOnceTimeIsUpAndEveryoneHasGone()
        {
            var settings = Settings(interval: 2f * Hour);
            settings.LastOrdersSeconds = 0.5f * Hour;
            var service = new ServiceSimulation(settings, seatCount: 3);
            Run(service, 2f * Hour + Minute);
            var customer = service.Customers.Single();
            service.Serve(customer.Seat, PlateWithFood());

            // A second customer arrives at 4 h; serve them too, then let time run out.
            Run(service, 2f * Hour);
            var second = service.Customers.Last();
            service.Serve(second.Seat, PlateWithFood());
            Run(service, 2f * Hour);

            Assert.That(service.IsClosed);
            Assert.That(service.Phase, Is.EqualTo(ServicePhase.Closed), "both are walking to the door");
            foreach (var c in service.Customers)
                service.Left(c);
            Assert.That(service.Phase, Is.EqualTo(ServicePhase.Finished));

            var elapsed = service.Elapsed;
            service.Tick(Hour);
            Assert.That(service.Elapsed, Is.EqualTo(elapsed), "nothing changes after the end");
        }

        [Test]
        public void EvenEndlessPatienceRunsOutAfterClosing()
        {
            var settings = Settings(interval: 3f * Hour, patience: 30f * Minute);
            var service = new ServiceSimulation(settings, seatCount: 3, patienceMultiplier: 0f);
            Run(service, 6f * Hour);
            var customer = service.Customers.First();
            Assert.That(customer.RanOutOfPatience, Is.False, "patient until closing");

            Run(service, 31f * Minute);
            Assert.That(customer.RanOutOfPatience);
        }

        [Test]
        public void TheSummaryAddsUpTheService()
        {
            var settings = Settings(interval: 12f * Minute, patience: 20f * Minute, queue: 0);
            settings.Menu = new OrderMenu
            {
                MainTypeId = "Steak",
                MainPrice = 24f,
                Choices = new[] { new OrderChoice(new Game.Simulation.Cooking.DonenessLevel("Rare", 45f, 48f), 1f) },
            };
            settings.Reviews = new ReviewSettings
            {
                Lines = new System.Collections.Generic.Dictionary<ReviewTopic, string[]> { { ReviewTopic.WalkedOut, new[] { "Left." } }, { ReviewTopic.Decent, new[] { "Fine." } } },
            };
            var service = new ServiceSimulation(settings, seatCount: 1);

            Run(service, 13f * Minute);
            var served = service.AtSeat(0);
            var score = new DishScore(System.Array.Empty<ItemScore>(), 0.7f, served.Order, null, OrderMatch.Matches);
            service.Serve(0, PlateWithFood(), score);
            service.Tick(16f * Minute); // pays and leaves; the arrival at 24 min takes the freed stool

            Run(service, 23f * Minute); // they run out of patience; later arrivals find no room
            var summary = ServiceSummary.From(service);

            Assert.That(summary.Served, Is.EqualTo(1));
            Assert.That(summary.AsOrdered, Is.EqualTo(1));
            Assert.That(summary.WalkOuts, Is.GreaterThanOrEqualTo(1));
            Assert.That(summary.TurnedAway, Is.GreaterThanOrEqualTo(1));
            Assert.That(summary.Tips, Is.EqualTo(service.Tips));
            Assert.That(summary.Money, Is.EqualTo(summary.Payments + summary.Tips - summary.WalkOutLosses));
            Assert.That(summary.Rating, Is.Not.Null);
            Assert.That(summary.ReviewCount, Is.EqualTo(service.Reviews.Count));
            Assert.That(summary.TopComplaint.Topic, Is.EqualTo(ReviewTopic.WalkedOut));
            Assert.That(summary.Worst.Stars, Is.LessThanOrEqualTo(summary.Best.Stars));
        }

        [Test]
        public void AnEmptyServiceSummarisesToNothing()
        {
            var summary = ServiceSummary.From(new ServiceSimulation(Settings(), seatCount: 3));
            Assert.That(summary.Served, Is.EqualTo(0));
            Assert.That(summary.Rating, Is.Null);
            Assert.That(summary.Best, Is.Null);
            Assert.That(summary.TopComplaint, Is.Null);
        }

        [Test]
        public void EmptySeatCantBeServed()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 3);
            Assert.That(service.Serve(0, PlateWithFood()), Is.False);
        }

        [Test]
        public void AfterEatingTheyLeaveAndFreeTheSeat()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 3);
            Run(service, 13f * Minute);
            var customer = service.Customers.Single();
            service.Serve(customer.Seat, PlateWithFood());

            service.Tick(16f * Minute);

            Assert.That(customer.State, Is.EqualTo(CustomerState.Leaving));
            Assert.That(service.AtSeat(customer.Seat), Is.Not.SameAs(customer));
            service.Left(customer);
            Assert.That(customer.State, Is.EqualTo(CustomerState.Gone));
        }

        [Test]
        public void AFreedSeatGoesToTheFirstInTheQueue()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 1);
            Run(service, 2f * 12f * Minute + Minute); // second customer queues
            var seated = service.AtSeat(0);
            var queued = service.Queue.Single();

            service.Serve(0, PlateWithFood());
            service.Tick(16f * Minute);

            Assert.That(service.AtSeat(0), Is.SameAs(queued));
            Assert.That(queued.State, Is.EqualTo(CustomerState.WalkingToSeat));
            Assert.That(seated.State, Is.EqualTo(CustomerState.Leaving));
        }

        [Test]
        public void TheSameSeedPlaysTheSameService()
        {
            var a = new ServiceSimulation(Settings(jitter: 0.35f), seatCount: 99);
            var b = new ServiceSimulation(Settings(jitter: 0.35f), seatCount: 99);
            a.Tick(6f * Hour);
            b.Tick(6f * Hour);
            Assert.That(a.Customers.Count, Is.EqualTo(b.Customers.Count));
        }

        [Test]
        public void BigAndSmallTicksGiveTheSameArrivals()
        {
            var small = new ServiceSimulation(Settings(jitter: 0.35f), seatCount: 99);
            var big = new ServiceSimulation(Settings(jitter: 0.35f), seatCount: 99);
            for (var i = 0; i < 360; i++)
                small.Tick(Minute);
            big.Tick(6f * Hour);
            Assert.That(small.Customers.Count, Is.EqualTo(big.Customers.Count));
        }

        [Test]
        public void SeatsBoughtBeforeServiceCount()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 3);
            service.OpenSeats(new[] { 1, 1, 1, 1, 1 });
            Assert.That(service.SeatCount, Is.EqualTo(5));
        }

        [Test]
        public void ACustomerWhoCantReachTheirSeatGivesUpAndItGoesToTheNext()
        {
            var service = new ServiceSimulation(Settings(), seatCount: 1);
            service.Tick(12f * Minute);
            var first = service.Customers[0];
            service.Tick(12f * Minute);
            var second = service.Customers[1];
            Assert.That(first.State, Is.EqualTo(CustomerState.WalkingToSeat));
            Assert.That(second.State, Is.EqualTo(CustomerState.Queueing));

            service.GiveUp(first);

            Assert.That(first.State, Is.EqualTo(CustomerState.Leaving));
            Assert.That(first.WalkedOut, Is.True);
            Assert.That(service.LostCovers, Is.EqualTo(1));
            Assert.That(service.Money, Is.EqualTo(0f), "no penalty: they never got to sit");
            Assert.That(second.State, Is.EqualTo(CustomerState.WalkingToSeat));
        }

        // Only tables for the given numbers of chairs, and one arrival per tick of the interval.
        private static ServiceSimulation Tables(params int[] tables)
        {
            var service = new ServiceSimulation(Settings(), seatCount: 1);
            service.OpenSeats(tables);
            return service;
        }

        private static IReadOnlyList<Customer> NextParty(ServiceSimulation service)
        {
            var count = service.Customers.Count;
            while (service.Customers.Count == count)
                service.Tick(1f);
            return service.Customers.Last().Party;
        }

        [Test]
        public void TheMixFollowsTheTables()
        {
            var layout = new[] { 1, 1, 1, 4 };
            Assert.That(PartyMix.Chance(1, layout), Is.EqualTo(0.8125f).Within(0.001f));
            Assert.That(PartyMix.Chance(4, layout), Is.EqualTo(0.0625f).Within(0.001f));
            Assert.That(PartyMix.Chance(5, layout), Is.EqualTo(0f));
            Assert.That(Enumerable.Range(1, 4).Sum(size => PartyMix.Chance(size, layout)), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void WithOnlySingleSeatsEveryoneComesAlone()
        {
            var random = new System.Random(1);
            for (var i = 0; i < 50; i++)
                Assert.That(PartyMix.PickSize(new[] { 1, 1, 1 }, random), Is.EqualTo(1));
        }

        [Test]
        public void TheBiggestTableSetsTheBiggestGroup()
        {
            var random = new System.Random(1);
            var sizes = Enumerable.Range(0, 400).Select(_ => PartyMix.PickSize(new[] { 3 }, random)).ToArray();
            Assert.That(sizes.Max(), Is.EqualTo(3));
            Assert.That(sizes.Min(), Is.EqualTo(1));
        }

        // The first group through the door, of a given size: which size comes depends on the seed.
        private static IReadOnlyList<Customer> FirstPartyOf(int size, out ServiceSimulation service, float patience, params int[] tables)
        {
            for (var seed = 1; ; seed++)
            {
                service = new ServiceSimulation(Settings(interval: 5f * Minute, patience: patience, seed: seed), seatCount: 1);
                service.OpenSeats(tables);
                var party = NextParty(service);
                if (party.Count == size)
                    return party;
            }
        }

        [Test]
        public void AGroupSitsTogetherAtOneTable()
        {
            var party = FirstPartyOf(3, out _, 24f * Hour, 1, 4);

            Assert.That(party.Select(c => c.Seat).Distinct().Count(), Is.EqualTo(3), "a chair each");
            Assert.That(party.All(c => c.Seat >= 1 && c.Seat <= 4), Is.True, "at the table for four");
            Assert.That(party.All(c => c.State == CustomerState.WalkingToSeat), Is.True);
        }

        [Test]
        public void SomeoneAloneTakesTheSmallestFreeTable()
        {
            var party = FirstPartyOf(1, out _, 24f * Hour, 4, 1, 2);
            Assert.That(party[0].Seat, Is.EqualTo(4), "the single seat, not the table for four");
        }

        [Test]
        public void GroupsDontShareATable()
        {
            var service = Tables(2);
            var first = NextParty(service);
            var second = NextParty(service);

            Assert.That(second.All(c => c.State == CustomerState.Queueing), Is.True, "one chair may be free, but it's taken");
            Assert.That(first.All(c => c.State == CustomerState.WalkingToSeat), Is.True);
        }

        [Test]
        public void AGroupLeavesTogetherAfterTheLastHasEaten()
        {
            var party = FirstPartyOf(2, out var service, 24f * Hour, 2);
            foreach (var customer in party)
                service.SitDown(customer);

            var eating = service.Settings.EatSeconds;
            service.Serve(party[0].Seat, PlateWithFood());
            service.Tick(eating / 2f);
            service.Serve(party[1].Seat, PlateWithFood());

            service.Tick(eating * 0.6f);
            Assert.That(party[0].State, Is.EqualTo(CustomerState.DoneEating), "finished, waiting for their friend");
            Assert.That(party[0].Bill, Is.Not.Null, "paid");
            Assert.That(party[1].State, Is.EqualTo(CustomerState.Eating));

            service.Tick(eating * 0.5f);
            Assert.That(party[0].State, Is.EqualTo(CustomerState.Leaving));
            Assert.That(party[1].State, Is.EqualTo(CustomerState.Leaving));
        }

        [Test]
        public void AGroupSharesOnePatienceMeterAndWalksOutTogether()
        {
            var patience = 40f * Minute;
            var party = FirstPartyOf(2, out var service, patience, 2);
            foreach (var customer in party)
                service.SitDown(customer);

            // Draining once, not twice: with two waiting it takes as long as for one.
            service.Tick(patience / 2f);
            Assert.That(party[0].Patience, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(party[1].Patience, Is.EqualTo(party[0].Patience));

            service.Serve(party[0].Seat, PlateWithFood());
            service.Tick(patience * 0.6f);
            Assert.That(party[1].RanOutOfPatience, Is.True);
            Assert.That(party[0].RanOutOfPatience, Is.False, "they were served");
            Assert.That(party[0].Bill, Is.Not.Null, "and paid for it");
            Assert.That(party.All(c => c.State == CustomerState.Leaving), Is.True);
        }

        [Test]
        public void ABigGroupInTheQueueDoesntKeepSomeoneAloneFromAFreeStool()
        {
            // The group waits for the table for four while someone alone takes the stool behind them.
            var party = FirstPartyOf(4, out var service, 24f * Hour, 4, 1);
            foreach (var member in party)
                service.GiveUp(member);
            Assert.That(party.All(c => c.State == CustomerState.Leaving), Is.True);
        }

        [Test]
        public void TheLongestWaitingGroupThatFitsGetsTheTable()
        {
            var service = new ServiceSimulation(Settings(queue: 9), seatCount: 1);
            service.OpenSeats(new[] { 1, 1 });
            var first = NextParty(service)[0];
            var second = NextParty(service)[0];
            var third = NextParty(service)[0];
            Assert.That(third.State, Is.EqualTo(CustomerState.Queueing));

            service.GiveUp(first);
            Assert.That(third.State, Is.EqualTo(CustomerState.WalkingToSeat));
            Assert.That(second.State, Is.EqualTo(CustomerState.WalkingToSeat));
        }
    }
}
