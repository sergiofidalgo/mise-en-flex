using System.Linq;
using Game.Data;
using Game.Simulation.Days;
using Game.Simulation.Items;
using Game.Simulation.Staff;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    public class StaffTests
    {
        private static Candidate Person(float skill = 1f, float speed = 1f, float wage = 40f, string name = "Ana") =>
            new Candidate(name, StaffRole.Dishwasher, "steady", skill, speed, wage);

        // A pass counter with a dirty plate waiting (or not).
        private sealed class FakePass : IDirtyPlateSource
        {
            public Plate Plate;
            public bool HasDirtyPlate => Plate != null;
            public Plate TakeDirtyPlate()
            {
                var plate = Plate;
                Plate = null;
                return plate;
            }
        }

        private static Plate Dirty()
        {
            var plate = new Plate(3);
            plate.MarkDirty();
            return plate;
        }

        private static (Dishwasher Job, Sink Sink, PlateStack Stack, FakePass Pass, DirtyPile Pile) Kitchen(Candidate person = null, StaffSettings settings = null, int seed = 1, int pileCapacity = 1000, int sinkCapacity = 1000)
        {
            var sink = new Sink(sinkCapacity);
            var pile = new DirtyPile(pileCapacity);
            var stack = new PlateStack(0, 3);
            var pass = new FakePass();
            var job = new Dishwasher(new StaffMember(person ?? Person()), sink, pile, stack, new IDirtyPlateSource[] { pass }, settings ?? new StaffSettings(), seed);
            return (job, sink, stack, pass, pile);
        }

        [Test]
        public void AFullLoopTakesAPlateFromThePassToTheStack()
        {
            var (job, sink, stack, pass, pile) = Kitchen();
            job.Tick(1f);
            Assert.That(job.Task, Is.EqualTo(DishwasherTask.Idle), "nothing to do yet");

            pass.Plate = Dirty();
            job.Tick(1f);
            Assert.That(job.Task, Is.EqualTo(DishwasherTask.ToPass));

            job.Arrived();
            Assert.That(pass.HasDirtyPlate, Is.False);
            Assert.That(job.Carried, Is.Not.Null);
            Assert.That(job.Task, Is.EqualTo(DishwasherTask.ToPile));

            job.Arrived();
            Assert.That(pile.Count, Is.EqualTo(1));
            Assert.That(job.Carried, Is.Null);

            job.Tick(1f); // goes to fetch it for the sink
            Assert.That(job.Task, Is.EqualTo(DishwasherTask.ToPile));
            job.Arrived();
            Assert.That(job.Carried, Is.Not.Null);
            Assert.That(pile.Count, Is.EqualTo(0));
            Assert.That(job.Task, Is.EqualTo(DishwasherTask.ToSink));
            job.Arrived();
            Assert.That(sink.DirtyCount, Is.EqualTo(1));

            job.Tick(1f); // decides to wash
            job.Arrived();
            Assert.That(job.Task, Is.EqualTo(DishwasherTask.Washing));

            job.Tick(149f);
            Assert.That(sink.CleanCount, Is.EqualTo(0));
            job.Tick(2f);
            Assert.That(sink.CleanCount, Is.EqualTo(1));
            Assert.That(job.Member.JobsToday, Is.EqualTo(1));

            job.Tick(1f);
            Assert.That(job.Task, Is.EqualTo(DishwasherTask.ToSink));
            job.Arrived();
            Assert.That(job.Task, Is.EqualTo(DishwasherTask.ToStack));
            Assert.That(job.Carried.IsDirty, Is.False);
            job.Arrived();
            Assert.That(stack.Count, Is.EqualTo(1));
        }

        [Test]
        public void TheyLeaveThePassAloneWhenThePileIsFullAndWashInstead()
        {
            var (job, sink, _, pass, pile) = Kitchen(pileCapacity: 1, sinkCapacity: 1);
            pile.Put(Dirty());
            sink.Put(Dirty());
            pass.Plate = Dirty();

            job.Tick(1f);
            Assert.That(job.Task, Is.EqualTo(DishwasherTask.ToSink), "washing makes room");
            Assert.That(pass.HasDirtyPlate);
        }

        [Test]
        public void IfThePileFillsUpOnTheWayTheyGoToTheSink()
        {
            var (job, sink, _, pass, pile) = Kitchen(pileCapacity: 1);
            pass.Plate = Dirty();
            job.Tick(1f);
            job.Arrived();
            pile.Put(Dirty()); // a chef got there first

            job.Arrived();
            Assert.That(job.Task, Is.EqualTo(DishwasherTask.ToSink));
            job.Arrived();
            Assert.That(sink.DirtyCount, Is.EqualTo(1));
            Assert.That(job.Carried, Is.Null);
        }

        [Test]
        public void WithEverythingFullTheyStillGetThrough()
        {
            var (job, sink, _, pass, pile) = Kitchen(pileCapacity: 1, sinkCapacity: 1);
            pass.Plate = Dirty();
            job.Tick(1f);
            job.Arrived();
            pile.Put(Dirty());
            sink.Put(Dirty());

            job.Arrived(); // the pile's full
            job.Arrived(); // the sink's full: they wash to make room
            Assert.That(job.Task, Is.EqualTo(DishwasherTask.Washing));

            job.Tick(151f);
            Assert.That(sink.CleanCount, Is.EqualTo(1));
            Assert.That(job.Carried, Is.Not.Null, "still holding theirs");
        }

        [Test]
        public void TheyLeaveAPlateOfFoodAlone()
        {
            var sink = new Sink();
            var counter = new PassCounter();
            var plate = new Plate(3);
            plate.Put(new Item("Steak"));
            counter.Put(plate);
            var job = new Dishwasher(new StaffMember(Person()), sink, new DirtyPile(6), new PlateStack(0, 3), new IDirtyPlateSource[] { counter }, new StaffSettings(), 1);

            job.Tick(1f);
            Assert.That(job.Task, Is.EqualTo(DishwasherTask.Idle));

            counter.StartEating();
            counter.FinishEating();
            job.Tick(1f);
            Assert.That(job.Task, Is.EqualTo(DishwasherTask.ToPass));
        }

        [Test]
        public void IfAPlayerGetsThereFirstTheyDoSomethingElse()
        {
            var (job, _, _, pass, _) = Kitchen();
            pass.Plate = Dirty();
            job.Tick(1f);
            pass.TakeDirtyPlate();
            job.Arrived();
            Assert.That(job.Task, Is.EqualTo(DishwasherTask.Idle));
            Assert.That(job.Carried, Is.Null);
        }

        [Test]
        public void FasterPeopleWashFaster()
        {
            float Seconds(float speed)
            {
                var (job, sink, _, _, _) = Kitchen(Person(speed: speed));
                sink.Put(Dirty());
                job.Tick(0f);
                job.Arrived();
                var seconds = 0f;
                while (sink.CleanCount == 0 && seconds < 1000f)
                {
                    job.Tick(1f);
                    seconds += 1f;
                }
                return seconds;
            }

            Assert.That(Seconds(1.4f), Is.LessThan(Seconds(1f)));
            Assert.That(Seconds(0.7f), Is.GreaterThan(Seconds(1f)));
        }

        [Test]
        public void FatigueSlowsThemDownAndARestFixesIt()
        {
            var settings = new StaffSettings();
            var member = new StaffMember(Person());
            var fresh = member.WorkingSpeed(settings);
            member.RecordJob(0.5f, mistake: false);
            Assert.That(member.WorkingSpeed(settings), Is.LessThan(fresh));
            Assert.That(member.MistakeChance(settings), Is.GreaterThan(0f), "tired people slip");

            member.StartDay();
            Assert.That(member.Fatigue, Is.EqualTo(0f));
            Assert.That(member.WorkingSpeed(settings), Is.EqualTo(fresh));
            Assert.That(member.JobsToday, Is.EqualTo(0));
        }

        [Test]
        public void CarelessPeopleBreakPlates()
        {
            int Broken(float skill)
            {
                var settings = new StaffSettings { WashSecondsPerPlate = 1f, FatigueMistakes = 0f, FatiguePerPlate = 0f };
                var (job, sink, stack, _, _) = Kitchen(Person(skill: skill), settings, seed: 3);
                for (var i = 0; i < 200; i++)
                    sink.Put(Dirty());
                for (var i = 0; i < 2000 && (sink.DirtyCount > 0 || job.Carried != null || sink.CleanCount > 0); i++)
                {
                    job.Tick(1f);
                    if (job.Task != DishwasherTask.Idle && job.Task != DishwasherTask.Washing)
                        job.Arrived();
                }
                Assert.That(stack.Count + sink.Broken, Is.EqualTo(200), "every plate is back or broken");
                return sink.Broken;
            }

            Assert.That(Broken(1f), Is.EqualTo(0));
            Assert.That(Broken(0.7f), Is.InRange(40, 80));
        }

        [Test]
        public void LettingSomeoneGoPutsTheirPlateBack()
        {
            var (job, _, _, pass, pile) = Kitchen();
            pass.Plate = Dirty();
            job.Tick(1f);
            job.Arrived();
            job.PutDownCarried();
            Assert.That(pile.Count, Is.EqualTo(1));
            Assert.That(job.Carried, Is.Null);
        }

        [Test]
        public void HiringNeedsTheMoneyAndAFreeRole()
        {
            var roster = new StaffRoster();
            var person = Person(wage: 40f);
            Assert.That(roster.CanHire(person, 39f), Is.False, "can't afford the first day");
            Assert.That(roster.CanHire(person, 40f));

            var hired = roster.Hire(person, 100f);
            Assert.That(roster.Hired, Has.Count.EqualTo(1));
            Assert.That(roster.TotalWages, Is.EqualTo(40f));
            Assert.That(roster.CanHire(Person(name: "Ben"), 1000f), Is.False, "one dishwasher for now");

            roster.Fire(hired);
            Assert.That(roster.Hired, Is.Empty);
            Assert.That(roster.TotalWages, Is.EqualTo(0f));
        }

        [Test]
        public void WagesComeOffTheBalance()
        {
            var days = new DayCycle(100f);
            Assert.That(days.Balance, Is.EqualTo(100f));
            days.Advance();
            days.Advance();
            days.FinishService(Game.Simulation.Service.ServiceSummary.From(
                new Game.Simulation.Service.ServiceSimulation(new Game.Simulation.Service.ServiceSettings(), seatCount: 1)), wages: 40f);
            Assert.That(days.Balance, Is.EqualTo(60f));
            Assert.That(days.LastWages, Is.EqualTo(40f));
        }

        [Test]
        public void TheConfigOffersRealCandidates()
        {
            var config = AssetDatabase.LoadAssetAtPath<StaffConfig>("Assets/_Project/Data/Settings/StaffConfig.asset");
            Assert.That(config, Is.Not.Null, "run Tools/Game/Build Greybox Kitchen");
            var candidates = config.CandidatesFor(1);
            Assert.That(candidates, Has.Count.EqualTo(3));
            Assert.That(candidates.All(c => !string.IsNullOrEmpty(c.Name) && !string.IsNullOrEmpty(c.Description)));
            Assert.That(candidates.Select(c => c.Name).Distinct().Count(), Is.EqualTo(3));
            Assert.That(candidates.Any(c => c.DailyWage <= config.StartingMoney), "day 1 has someone you can afford");

            Assert.That(config.CandidatesFor(1).Select(c => c.Name), Is.EqualTo(candidates.Select(c => c.Name)), "the same day repeats");

            // Checks the saved asset, not just the defaults in code: the two can differ.
            for (var day = 1; day <= 30; day++)
            {
                var offered = config.CandidatesFor(day);
                Assert.That(offered.Select(c => c.Role).Distinct().Count(), Is.EqualTo(System.Enum.GetValues(typeof(StaffRole)).Length), $"day {day} offers every role");
            }
        }
    }
}
