using System.Linq;
using Game.Simulation.Cleaning;
using Game.Simulation.Saving;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>Dirt is saved overnight with the restaurant (decision 2026-11-06).</summary>
    public class DirtSaveTests
    {
        [Test]
        public void DirtComesBackWhereItWasOnTheSameFurnitureAndPartlyWiped()
        {
            var table = new object();
            var counter = new object();
            var dirt = new DirtMap();
            var mess = new MessMaker(dirt);
            mess.DinerFinished(DirtSurface.Table, table, 0.3f, -0.2f);
            mess.PanBurnt(counter, 0.1f, 0f);
            var spill = mess.PlateBroken(2f, 3f);
            dirt.Clean(spill, 1f);

            var save = new CampaignSave();
            DirtRecords.Write(dirt, host => host == table ? 0 : host == counter ? 1 : -1, save.Dirt);
            var json = JsonUtility.ToJson(save);
            var back = JsonUtility.FromJson<CampaignSave>(json);

            var newTable = new object();
            var newCounter = new object();
            var morning = new DirtMap();
            DirtRecords.Read(back.Dirt, i => i == 0 ? newTable : i == 1 ? newCounter : null, morning);

            Assert.That(morning.Spots.Count, Is.EqualTo(3));
            var onTable = morning.On(newTable).Single();
            Assert.That((onTable.Kind, onTable.X, onTable.Z), Is.EqualTo((DirtKind.Smudge, 0.3f, -0.2f)));
            Assert.That(morning.On(newCounter).Single().Kind, Is.EqualTo(DirtKind.Scorch));
            var floor = morning.Spots.Single(s => s.Surface == DirtSurface.Floor);
            Assert.That(floor.Amount, Is.EqualTo(spill.Amount));
            Assert.That(floor.Remaining, Is.EqualTo(spill.Remaining).Within(1e-4f));
            Assert.That(morning.Total, Is.EqualTo(dirt.Total).Within(1e-4f));
        }

        [Test]
        public void DirtOnFurnitureThatIsGoneIsDropped()
        {
            var dirt = new DirtMap();
            new MessMaker(dirt).DinerFinished(DirtSurface.Table, new object(), 0f, 0f);
            var save = new CampaignSave();
            DirtRecords.Write(dirt, _ => -1, save.Dirt);
            Assert.That(save.Dirt, Is.Empty);

            save.Dirt.Add(new DirtRecord { Kind = 0, Surface = (int)DirtSurface.Table, Station = 5, Amount = 1f, Peak = 1f });
            var morning = new DirtMap();
            DirtRecords.Read(save.Dirt, _ => null, morning);
            Assert.That(morning.Spots, Is.Empty);
        }

        [Test]
        public void AFileFromBeforeDirtLoadsClean()
        {
            var save = JsonUtility.FromJson<CampaignSave>("{\"Version\":1,\"Day\":2,\"Balance\":10}");
            Assert.That(save.Dirt, Is.Not.Null.And.Empty);
            var morning = new DirtMap();
            DirtRecords.Read(save.Dirt, _ => null, morning);
            Assert.That(morning.Spots, Is.Empty);
        }
    }
}
