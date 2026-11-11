using System;
using System.Linq;
using Game.Simulation.Cleaning;
using Game.Simulation.Items;
using Game.Simulation.Quality;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The room around the guests (decisions 2026-11-11): the wheelie bin by the customer door
    /// smells, softly and by distance; an open kitchen is a show on a great night and a nuisance on
    /// any other, and always makes more mess.
    /// </summary>
    public class SurroundingsTests
    {
        private const float Far = float.PositiveInfinity;
        private const float LowPatience = 0.25f;
        private readonly SurroundingsSettings _settings = new SurroundingsSettings();

        private static DishScore Good() => new DishScore(Array.Empty<ItemScore>(), 0.9f);

        // ---- Bin odour ----

        [Test]
        public void TheBinCostsMostAtTheDoorAndFadesToNothingAtTheRadius()
        {
            Assert.That(Surroundings.BinSmell(0f, _settings), Is.EqualTo(0.2f).Within(1e-5f), "up to 1 star, the user's call");
            Assert.That(Surroundings.BinSmell(2f, _settings), Is.EqualTo(0.1f).Within(1e-5f), "linear: half way, half the cost");
            Assert.That(Surroundings.BinSmell(4f, _settings), Is.EqualTo(0f), "4 m out, nothing");
            Assert.That(Surroundings.BinSmell(6f, _settings), Is.EqualTo(0f));
            Assert.That(Surroundings.BinSmell(Far, _settings), Is.EqualTo(0f), "no bin, no smell");
        }

        [Test]
        public void MovingTheBinAwayAlwaysHelps()
        {
            var last = float.MaxValue;
            for (var metres = 0f; metres <= 5f; metres += 0.25f)
            {
                var smell = Surroundings.BinSmell(metres, _settings);
                Assert.That(smell, Is.LessThanOrEqualTo(last), $"at {metres} m");
                last = smell;
            }
        }

        [Test]
        public void TheRadiusAndStrengthAreTuningValues()
        {
            var wide = new SurroundingsSettings { BinSmellRadius = 8f, BinSmellMaxPenalty = 0.1f };
            Assert.That(Surroundings.BinSmell(6f, wide), Is.EqualTo(0.025f).Within(1e-5f));
            Assert.That(Surroundings.BinSmell(0f, new SurroundingsSettings { BinSmellRadius = 0f }), Is.EqualTo(0f), "a zero radius turns it off");
        }

        [Test]
        public void TheSmellComesOffTheScoreAndOnlyAStrongOneIsReviewed()
        {
            var reviews = new ReviewSettings();
            var close = Good().WithSurroundings(Surroundings.For(0.5f, 0, 0, 5f, 1f, LowPatience, _settings));
            Assert.That(close.Overall, Is.EqualTo(0.9f - 0.175f).Within(1e-4f));
            Assert.That(ReviewWriter.ChooseTopic(close, 1f, reviews), Is.EqualTo(ReviewTopic.BinSmell));

            var faint = Good().WithSurroundings(Surroundings.For(3f, 0, 0, 5f, 1f, LowPatience, _settings));
            Assert.That(faint.Overall, Is.LessThan(0.9f), "a faint smell still costs a little");
            Assert.That(ReviewWriter.ChooseTopic(faint, 1f, reviews), Is.Not.EqualTo(ReviewTopic.BinSmell), "but isn't worth a review");
        }

        [Test]
        public void TheFoodAndTheTableAreRememberedBeforeTheBin()
        {
            var reviews = new ReviewSettings();
            var smell = Surroundings.For(0f, 0, 0, 5f, 1f, LowPatience, _settings);
            Assert.That(ReviewWriter.ChooseTopic(Good().WithDirt(0.1f, 0.05f).WithSurroundings(smell), 1f, reviews), Is.EqualTo(ReviewTopic.Dirty));
            Assert.That(ReviewWriter.ChooseTopic(Good().WithSurroundings(smell), 0f, reviews), Is.EqualTo(ReviewTopic.BinSmell), "the smell beats a slow wait");
        }

        [Test]
        public void TheSmellIsNeverADisaster()
        {
            var worst = Good().WithSurroundings(Surroundings.For(0f, 0, 0, 5f, 1f, LowPatience, _settings));
            Assert.That(Good().Stars - worst.Stars, Is.LessThanOrEqualTo(1f), "at most a star");
            var poor = new DishScore(Array.Empty<ItemScore>(), 0.1f).WithSurroundings(Surroundings.For(0f, 0, 0, 0f, 1f, LowPatience, _settings));
            Assert.That(poor.Overall, Is.EqualTo(0f), "never below zero");
        }

        // ---- Open kitchen ----

        [Test]
        public void KeptApartTheKitchenDoesNothing()
        {
            var effect = Surroundings.For(Far, 0, 0, 5f, 1f, LowPatience, _settings);
            Assert.That(effect.Reaction, Is.EqualTo(OpenKitchenReaction.None));
            Assert.That(Good().WithSurroundings(effect).Overall, Is.EqualTo(0.9f));
        }

        [Test]
        public void AGreatNightIsAShowGuestsLove()
        {
            var effect = Surroundings.For(Far, 1, 0, 4.5f, 0.8f, LowPatience, _settings);
            Assert.That(effect.Reaction, Is.EqualTo(OpenKitchenReaction.Show));
            Assert.That(effect.OpenKitchen, Is.EqualTo(0.1f).Within(1e-5f), "half a star a piece");
            var score = new DishScore(Array.Empty<ItemScore>(), 0.85f).WithSurroundings(effect);
            Assert.That(score.Overall, Is.EqualTo(0.95f).Within(1e-4f));
            Assert.That(ReviewWriter.ChooseTopic(score, 1f, new ReviewSettings()), Is.EqualTo(ReviewTopic.ChefsTable));
        }

        [Test]
        public void AnAverageOrPoorNightBringsComplaintsAboutMessAndNoise()
        {
            var average = Surroundings.For(Far, 0, 1, 4f, 1f, LowPatience, _settings);
            Assert.That(average.Reaction, Is.EqualTo(OpenKitchenReaction.Complaint));
            Assert.That(average.OpenKitchen, Is.EqualTo(-0.1f).Within(1e-5f));
            var score = Good().WithSurroundings(average);
            Assert.That(score.Overall, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(ReviewWriter.ChooseTopic(score, 1f, new ReviewSettings()), Is.EqualTo(ReviewTopic.OpenKitchenMess));
        }

        [Test]
        public void ALongWaitSpoilsTheShowEvenOnAGreatNight()
        {
            Assert.That(Surroundings.IsGreatNight(5f, 0.2f, LowPatience, _settings), Is.False);
            Assert.That(Surroundings.For(Far, 1, 0, 5f, 0.2f, LowPatience, _settings).Reaction, Is.EqualTo(OpenKitchenReaction.Complaint));
            Assert.That(Surroundings.IsGreatNight(4.5f, LowPatience, LowPatience, _settings), Is.True, "the marks themselves count");
        }

        [Test]
        public void MorePiecesMoveTheScoreMoreUpToOneStar()
        {
            Assert.That(Surroundings.OpenKitchenSize(1, 0, _settings), Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(Surroundings.OpenKitchenSize(1, 1, _settings), Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(Surroundings.OpenKitchenSize(3, 2, _settings), Is.EqualTo(0.2f).Within(1e-5f), "capped at a star");
            Assert.That(Surroundings.OpenKitchenSize(0, 0, _settings), Is.EqualTo(0f));
        }

        [Test]
        public void AShowOnlyMakesAPraiseWorthyMealAChefsTableReview()
        {
            var show = Surroundings.For(Far, 1, 0, 5f, 1f, LowPatience, _settings);
            var middling = new DishScore(Array.Empty<ItemScore>(), 0.6f).WithSurroundings(show);
            Assert.That(ReviewWriter.ChooseTopic(middling, 1f, new ReviewSettings()), Is.EqualTo(ReviewTopic.Decent));
        }

        [Test]
        public void TheChefsTableIsPraiseAndTheMessIsAComplaint()
        {
            Assert.That(ReviewReplies.IsComplaint(ReviewTopic.ChefsTable), Is.False);
            Assert.That(ReviewReplies.IsComplaint(ReviewTopic.OpenKitchenMess), Is.True);
            Assert.That(ReviewReplies.IsComplaint(ReviewTopic.BinSmell), Is.True);
            Assert.That(new ReviewSettings { PraiseShare = 0.9f, ComplaintShare = 0.3f }.ShareChance(ReviewTopic.ChefsTable, Persona.Regular), Is.EqualTo(0.9f));
        }

        [Test]
        public void ApplyingAgainReplacesTheEffectRatherThanAddingToIt()
        {
            var smell = Surroundings.For(0f, 0, 0, 5f, 1f, LowPatience, _settings);
            var once = Good().WithSurroundings(smell);
            var twice = once.WithSurroundings(smell);
            Assert.That(twice.Overall, Is.EqualTo(once.Overall).Within(1e-5f));
            Assert.That(twice.OverallBeforeSurroundings, Is.EqualTo(0.9f).Within(1e-5f));
        }

        // ---- How good the night is ----

        [Test]
        public void TheNightIsJudgedOnTheFoodSoFarWithTheGuestsOwnDish()
        {
            var service = new ServiceSimulation(new ServiceSettings
            {
                PatienceSeconds = 24f * 3600f, DurationSeconds = 6f * 3600f, ArrivalIntervalSeconds = 720f,
                RushMultiplier = 1f, EatSeconds = 900f, MaxQueue = 3, Seed = 7,
            }, seatCount: 3);
            for (var t = 0f; t < 2f * 720f + 60f; t += 60f)
            {
                service.Tick(60f);
                foreach (var c in service.Customers.Where(c => c.State == CustomerState.WalkingToSeat).ToList())
                    service.SitDown(c);
            }
            Assert.That(Surroundings.NightStars(service.Customers, 4f), Is.EqualTo(4f), "the first guest judges by their own plate");

            // The first guest's plate was great, but a bin took a star off it: the night still counts it as great food.
            var plate = new Plate(3);
            plate.Put(new Item("Steak"));
            var smelly = new DishScore(Array.Empty<ItemScore>(), 1f).WithSurroundings(Surroundings.For(0f, 0, 0, 5f, 1f, LowPatience, _settings));
            service.Serve(0, plate, smelly);
            Assert.That(service.AtSeat(0).RatedStars, Is.EqualTo(4f));
            Assert.That(service.AtSeat(0).StarsBeforeSurroundings, Is.EqualTo(5f));
            Assert.That(Surroundings.NightStars(service.Customers, 4f), Is.EqualTo(4.5f));
        }

        // ---- More mess ----

        [Test]
        public void PiecesOutOfPlaceLeaveMessEveryFewGuests()
        {
            var dirt = new DirtMap(new DirtSettings { GuestsPerOpenKitchenMess = 3, OpenKitchenMess = 1f });
            var mess = new MessMaker(dirt);
            var pieces = new[]
            {
                new OutOfPlacePiece(OutOfPlaceKind.ApplianceInDiningRoom, 0f, 0f),
                new OutOfPlacePiece(OutOfPlaceKind.TableInKitchen, 5f, 5f),
            };
            Assert.That(mess.OpenKitchenGuest(pieces), Is.Empty);
            Assert.That(mess.OpenKitchenGuest(pieces), Is.Empty);
            var made = mess.OpenKitchenGuest(pieces);
            Assert.That(made.Count, Is.EqualTo(2), "one spot by each piece on the third guest");
            Assert.That(made.All(s => s.Surface == DirtSurface.Floor));
            Assert.That(made[0].Kind, Is.EqualTo(DirtKind.Scorch), "grease by the appliance");
            Assert.That(made[1].Kind, Is.EqualTo(DirtKind.Scuff), "footprints by the table");
            Assert.That(dirt.Total, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void AKitchenKeptApartMakesNoExtraMess()
        {
            var dirt = new DirtMap(new DirtSettings { GuestsPerOpenKitchenMess = 1 });
            var mess = new MessMaker(dirt);
            for (var i = 0; i < 10; i++)
                Assert.That(mess.OpenKitchenGuest(Array.Empty<OutOfPlacePiece>()), Is.Empty);
            Assert.That(dirt.Total, Is.EqualTo(0f));
        }

        [Test]
        public void TheHygieneDialScalesTheOpenKitchenMess()
        {
            var dirt = new DirtMap(new DirtSettings { GuestsPerOpenKitchenMess = 1, OpenKitchenMess = 2f });
            var mess = new MessMaker(dirt) { Hygiene = 0.5f };
            mess.OpenKitchenGuest(new[] { new OutOfPlacePiece(OutOfPlaceKind.ApplianceInDiningRoom, 0f, 0f) });
            Assert.That(dirt.Total, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void CountingTellsAppliancesFromTables()
        {
            var (appliances, tables) = Surroundings.Count(new[]
            {
                new OutOfPlacePiece(OutOfPlaceKind.ApplianceInDiningRoom, 0f, 0f),
                new OutOfPlacePiece(OutOfPlaceKind.ApplianceInDiningRoom, 1f, 0f),
                new OutOfPlacePiece(OutOfPlaceKind.TableInKitchen, 2f, 0f),
            });
            Assert.That(appliances, Is.EqualTo(2));
            Assert.That(tables, Is.EqualTo(1));
            Assert.That(Surroundings.Count(null), Is.EqualTo((0, 0)));
        }
    }
}
