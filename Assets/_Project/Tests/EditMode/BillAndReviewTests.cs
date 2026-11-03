using System;
using System.Linq;
using Game.Data;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Quality;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    public class BillAndReviewTests
    {
        private const float Minute = 60f;

        private static readonly DonenessLevel MediumRare = new DonenessLevel("Medium-rare", 52f, 54.5f);
        private static readonly DonenessLevel Medium = new DonenessLevel("Medium", 57f, 60f);

        private static Order SteakOrder(string side = null) =>
            new Order("Steak", MediumRare, side) { MainName = "Steak", SideName = "Onions", MainPrice = 24f, SidePrice = 4f };

        private static Ingredient Steak(float peakCore, bool burnt = false)
        {
            var steak = new CookingSimulation(22f).AddIngredient(new IngredientProperties
            {
                TypeId = "Steak",
                HeatCapacity = 1050f,
                SurfaceFraction = 0.15f,
                DonenessLevels = new[] { MediumRare, Medium },
                StartTemperature = peakCore,
                TargetCoreTemperature = 54.5f,
                BurntBrowning = burnt ? 0f : 2f,
            });
            return steak;
        }

        private static Plate PlateOf(params Item[] items)
        {
            var plate = new Plate(3);
            foreach (var item in items)
                plate.Put(item);
            return plate;
        }

        private static DishScore Score(float overall, Order order, params string[] missing) =>
            new DishScore(Array.Empty<ItemScore>(), overall, order, missing);

        [Test]
        public void ABetterDishPaysMore()
        {
            var settings = new PaymentSettings();
            var order = SteakOrder();
            var great = Billing.Calculate(order, Score(1f, order), 1f, settings);
            var poor = Billing.Calculate(order, Score(0.2f, order), 1f, settings);

            Assert.That(great.Payment, Is.EqualTo(24f));
            Assert.That(poor.Payment, Is.GreaterThan(0f).And.LessThan(great.Payment), "a bad dish still earns something");
        }

        [TestCase(1.00f, 24f)] // perfect: a whole extra price
        [TestCase(0.995f, 24f)] // counts as perfect
        [TestCase(0.9925f, 18f)]
        [TestCase(0.99f, 12f)] // one point off costs half
        [TestCase(0.95f, 5f)]
        [TestCase(0.90f, 2f)]
        [TestCase(0.80f, 0f)]
        [TestCase(0.50f, 0f)]
        public void TipsFallSteeplyFromPerfect(float score, float tip)
        {
            var order = SteakOrder();
            Assert.That(Billing.Calculate(order, Score(score, order), 1f, new PaymentSettings()).Tip, Is.EqualTo(tip));
        }

        [Test]
        public void TheServiceConfigCurveCountsNearPerfectAsPerfect()
        {
            var config = AssetDatabase.LoadAssetAtPath<ServiceConfig>("Assets/_Project/Data/Settings/ServiceConfig.asset");
            var order = SteakOrder();
            var payment = config.ToSettings().Payment;
            Assert.That(Billing.Calculate(order, Score(0.995f, order), 1f, payment).Tip, Is.EqualTo(24f));
            Assert.That(Billing.Calculate(order, Score(0.99f, order), 1f, payment).Tip, Is.EqualTo(12f));
        }

        [Test]
        public void TheTipIsOnTheMenuPriceNotWhatTheyPaid()
        {
            var order = SteakOrder("DicedOnion");
            Assert.That(Billing.Calculate(order, Score(1f, order), 1f, new PaymentSettings()).Tip, Is.EqualTo(28f));
        }

        [Test]
        public void OnlyALongWaitReducesTheTip()
        {
            var settings = new PaymentSettings();
            var order = SteakOrder();
            float Tip(float patience) => Billing.Calculate(order, Score(1f, order), patience, settings).Tip;

            Assert.That(Tip(1f), Is.EqualTo(24f));
            Assert.That(Tip(0.5f), Is.EqualTo(24f), "patience to spare doesn't cost anything");
            Assert.That(Tip(0.25f), Is.LessThan(24f).And.GreaterThan(Tip(0f)));
            Assert.That(Tip(0f), Is.EqualTo(10f), "40% of 24, rounded");
        }

        [Test]
        public void MissingItemsArentPaidFor()
        {
            var settings = new PaymentSettings();
            var order = SteakOrder("DicedOnion");
            var full = Billing.Calculate(order, Score(1f, order), 1f, settings);
            var noSide = Billing.Calculate(order, Score(1f, order, "DicedOnion"), 1f, settings);
            var noMain = Billing.Calculate(order, Score(1f, order, "Steak"), 1f, settings);

            Assert.That(full.Payment, Is.EqualTo(28f));
            Assert.That(noSide.Payment, Is.EqualTo(24f));
            Assert.That(noMain.Payment, Is.EqualTo(4f));
        }

        [Test]
        public void NoOrderOrNoScoreMeansNoMoney()
        {
            var settings = new PaymentSettings();
            Assert.That(Billing.Calculate(null, Score(1f, null), 1f, settings).Total, Is.EqualTo(0f));
            Assert.That(Billing.Calculate(SteakOrder(), null, 1f, settings).Total, Is.EqualTo(0f));
        }

        private static ReviewTopic Topic(Plate plate, Order order, float patience = 1f) =>
            ReviewWriter.ChooseTopic(DishScorer.Score(plate, new ScoringSettings(), order), patience, new ReviewSettings());

        [Test]
        public void ReviewsComplainAboutTheWorstThing()
        {
            Assert.That(Topic(PlateOf(Steak(70f)), SteakOrder()), Is.EqualTo(ReviewTopic.Overcooked));
            Assert.That(Topic(PlateOf(Steak(30f)), SteakOrder()), Is.EqualTo(ReviewTopic.Undercooked));
            Assert.That(Topic(PlateOf(Steak(54.5f)), SteakOrder("DicedOnion")), Is.EqualTo(ReviewTopic.NoSide));
            Assert.That(Topic(PlateOf(Steak(54.5f, burnt: true)), SteakOrder()), Is.EqualTo(ReviewTopic.Burnt));
        }

        [Test]
        public void GoodFoodAfterALongWaitMentionsTheWait()
        {
            var prompt = Topic(PlateOf(Steak(54.5f)), SteakOrder());
            var slow = Topic(PlateOf(Steak(54.5f)), SteakOrder(), patience: 0.1f);
            Assert.That(prompt, Is.Not.EqualTo(ReviewTopic.SlowService));
            Assert.That(slow, Is.EqualTo(ReviewTopic.SlowService));
        }

        private static ServiceSimulation Service(float patience = 3600f * 24f) => new ServiceSimulation(new ServiceSettings
        {
            DurationSeconds = 6f * 3600f,
            ArrivalIntervalSeconds = 12f * Minute,
            ArrivalJitter = 0f,
            RushMultiplier = 1f,
            EatSeconds = 15f * Minute,
            PatienceSeconds = patience,
            Menu = new OrderMenu
            {
                MainTypeId = "Steak",
                MainName = "Steak",
                MainPrice = 24f,
                Choices = new[] { new OrderChoice(MediumRare, 1f) },
            },
            Reviews = new ReviewSettings
            {
                Lines = new System.Collections.Generic.Dictionary<ReviewTopic, string[]>
                {
                    { ReviewTopic.Overcooked, new[] { "Asked for {ordered}, got {got}." } },
                    { ReviewTopic.WalkedOut, new[] { "Left hungry." } },
                },
            },
        }, seatCount: 3);

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
        public void CustomersPayAndReviewWhenTheyFinishEating()
        {
            var service = Service();
            Run(service, 13f * Minute);
            var customer = service.AtSeat(0);
            var plate = PlateOf(Steak(70f));
            service.Serve(0, plate, DishScorer.Score(plate, new ScoringSettings(), customer.Order));
            Assert.That(customer.Bill, Is.Null, "pays when finished");
            Assert.That(service.Money, Is.EqualTo(0f));

            Customer paid = null;
            service.Paid += c => paid = c;
            service.Tick(16f * Minute);

            Assert.That(paid, Is.SameAs(customer));
            Assert.That(customer.Bill, Is.Not.Null);
            Assert.That(service.Money, Is.EqualTo(customer.Bill.Value.Total).And.GreaterThan(0f));
            Assert.That(service.Reviews.Single().Line, Is.EqualTo("Asked for medium-rare, got medium."));
        }

        [Test]
        public void WalkOutsCostMoneyAndLeaveAOneStarReview()
        {
            var service = Service(patience: 10f * Minute);
            Run(service, 13f * Minute);
            var customer = service.AtSeat(0);
            Run(service, 11f * Minute);

            Assert.That(customer.RanOutOfPatience);
            Assert.That(customer.Bill, Is.Null);
            Assert.That(customer.WalkOutPenalty, Is.EqualTo(12f), "half of the 24 steak");
            Assert.That(service.Money, Is.EqualTo(-12f));
            Assert.That(service.WalkOutLosses, Is.EqualTo(12f));
            Assert.That(customer.Review.Topic, Is.EqualTo(ReviewTopic.WalkedOut));
            Assert.That(customer.Review.Stars, Is.EqualTo(1f));
            Assert.That(customer.Review.Line, Is.EqualTo("Left hungry."));
        }

        [Test]
        public void TurnedAwayCustomersCostNothing()
        {
            var service = new ServiceSimulation(new ServiceSettings
            {
                ArrivalIntervalSeconds = 12f * Minute,
                ArrivalJitter = 0f,
                MaxQueue = 0,
                PatienceSeconds = 3600f * 24f,
                Menu = new OrderMenu { MainTypeId = "Steak", MainPrice = 24f, Choices = new[] { new OrderChoice(MediumRare, 1f) } },
            }, seatCount: 0);
            service.Tick(13f * Minute);

            Assert.That(service.LostCovers, Is.EqualTo(1));
            Assert.That(service.Money, Is.EqualTo(0f));
        }

        // Real assets, best play: a top tip has to be reachable for each doneness on the menu, or the
        // top of the curve is a lie. Searches heat, time per side and rest like a skilled cook would.
        [TestCase("Rare", 48f)]
        [TestCase("Medium-rare", 54.5f)]
        [TestCase("Medium", 60f)]
        [TestCase("Well done", 71f)]
        public void AWellPlayedSteakCanReachTheTopOfTheTipCurve(string doneness, float target)
        {
            var steakDefinition = AssetDatabase.LoadAssetAtPath<IngredientDefinition>("Assets/_Project/Data/Ingredients/ING_Steak.asset");
            var equipment = AssetDatabase.LoadAssetAtPath<EquipmentTier>("Assets/_Project/Data/Settings/EquipmentTier_Basic.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<TuningConfig>("Assets/_Project/Data/Settings/TuningConfig.asset");
            var realism = AssetDatabase.LoadAssetAtPath<RealismSettings>("Assets/_Project/Data/Settings/RealismSettings.asset");
            var scoring = tuning.Quality(realism.CookingPrecision);
            var properties = steakDefinition.ToProperties();
            var level = properties.DonenessLevels.First(l => l.Name == doneness);
            Assert.That(level.OrderTargetCoreTemperature, Is.EqualTo(target));
            var order = new Order("Steak", level);

            var best = 0f;
            var bestAt = string.Empty;
            foreach (var dial in new[] { 1f, 0.8f, 0.6f, 0.45f })
            {
                for (var side = 60f; side <= 480f; side += 15f)
                {
                    for (var rest = 0f; rest <= 300f; rest += 30f)
                    {
                        var sim = new CookingSimulation(tuning.AmbientTemperature);
                        var hob = sim.AddHob(equipment.Hob);
                        var pan = sim.AddPan(equipment.Pan);
                        hob.PlacePan(pan);
                        hob.Dial = dial;
                        sim.Tick(4f * Minute);
                        var steak = sim.AddIngredient(properties);
                        pan.Put(steak);
                        sim.Tick(side);
                        steak.Flip();
                        sim.Tick(side);
                        pan.Take();
                        var plate = new Plate(3);
                        plate.Put(steak);
                        sim.Tick(rest);
                        var overall = DishScorer.Score(plate, scoring, order).Overall;
                        if (overall > best)
                        {
                            best = overall;
                            bestAt = $"dial {dial}, {side}s a side, {rest}s rest";
                        }
                    }
                }
            }

            UnityEngine.Debug.Log($"[TipReach] {doneness}: best {best:0.0000} at {bestAt}");
            Assert.That(best, Is.GreaterThanOrEqualTo(0.99f), $"{doneness}: best was {best:0.0000} at {bestAt}");
        }

        [Test]
        public void ServiceConfigHasALineForEveryTopic()
        {
            var config = AssetDatabase.LoadAssetAtPath<ServiceConfig>("Assets/_Project/Data/Settings/ServiceConfig.asset");
            var lines = config.ToSettings().Reviews.Lines;
            foreach (ReviewTopic topic in Enum.GetValues(typeof(ReviewTopic)))
                Assert.That(lines.TryGetValue(topic, out var options) && options.Length > 0, topic.ToString());
        }
    }
}
