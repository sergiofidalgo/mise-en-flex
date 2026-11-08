using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The plate, plate rack, sink and cutting board models fit the sizes the greybox gave them, so
    /// swapping them in changes nothing about play.
    /// </summary>
    public class KitchenwareArtTests
    {
        private const string ModelsFolder = "Assets/_Project/Art/Models";
        private const float Tolerance = 0.005f;

        private static Bounds Model(string file, string meshName)
        {
            var path = $"{ModelsFolder}/{file}.fbx";
            var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(m => m.name == meshName);
            Assert.That(mesh, Is.Not.Null, $"{path} has no mesh called {meshName}");
            return mesh.bounds;
        }

        private static GameObject Prefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        [Test]
        public void PlateIsTheGreyboxPlatesWidthWithItsOriginAtItsBase()
        {
            var body = Model("SM_Plate", "PlateBody");
            var rim = Model("SM_Plate", "PlateRim");
            // The fat rim is the plate's outside edge.
            Assert.That(rim.size.x, Is.EqualTo(0.32f).Within(Tolerance));
            Assert.That(rim.size.z, Is.EqualTo(0.32f).Within(Tolerance));
            Assert.That(body.size.x, Is.LessThanOrEqualTo(0.32f));
            Assert.That(body.min.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(body.max.y, Is.LessThanOrEqualTo(0.042f), "a thick, cartoony plate, but one a pile's step high");
            Assert.That(Model("SM_Plate", "PlateRim").max.y, Is.LessThanOrEqualTo(0.042f + Tolerance), "piles sit rim on rim");


        }

        [Test]
        public void CarriedPlateHasTheModelAndItsRim()
        {
            var body = Prefab("Assets/_Project/Prefabs/Items/PF_Item_Plate.prefab").transform.Find("Body");
            Assert.That(body.GetComponent<MeshFilter>().sharedMesh.name, Is.EqualTo("PlateBody"));
            Assert.That(body.Find("Rim"), Is.Not.Null);
            Assert.That(body.localPosition, Is.EqualTo(Vector3.zero), "Its base sits where the greybox plate's did.");
        }

        [Test]
        public void PlateRackHoldsTheStacksSixPlatesAndFitsTheCounter()
        {
            var rack = Model("SM_PlateRack", "PlateRack");
            Assert.That(rack.size.z, Is.GreaterThanOrEqualTo(5 * 0.06f), "Room for six plates, one behind the other.");
            Assert.That(rack.size.x, Is.LessThan(1f));
            Assert.That(rack.min.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void SinkFillsItsStationWithTheTopAtCounterHeight()
        {
            var sink = Model("SM_Sink", "SinkBody");
            Assert.That(sink.size.x, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(sink.size.z, Is.LessThanOrEqualTo(1f + Tolerance));
            Assert.That(sink.min.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(sink.max.y, Is.GreaterThan(0.9f), "The tap and upstand rise above the top.");
        }

        [Test]
        public void BoardKeepsItsSizeAndSitsOnItsCounter()
        {
            var board = Model("SM_Board", "BoardBody");
            Assert.That(board.size.x, Is.EqualTo(0.7f).Within(Tolerance), "most of its 1 m counter");
            Assert.That(board.size.z, Is.EqualTo(0.55f).Within(Tolerance));
            Assert.That(board.size.y, Is.EqualTo(0.04f).Within(Tolerance));
            Assert.That(board.min.y, Is.EqualTo(0f).Within(Tolerance));

            var counter = Prefab("Assets/_Project/Prefabs/Furniture/PF_CuttingBoard.prefab");
            var piece = counter.transform.Find("Board");
            Assert.That(piece.GetComponent<MeshFilter>().sharedMesh.name, Is.EqualTo("BoardBody"));
            Assert.That(piece.localPosition.y, Is.EqualTo(0.9f).Within(Tolerance), "Its base on the counter top.");
            Assert.That(piece.localScale, Is.EqualTo(Vector3.one));
            Assert.That(counter.transform.Find("BoardAnchor").localPosition.y, Is.EqualTo(0.9f + board.size.y).Within(Tolerance),
                "Food on the board sits on its surface.");
        }
    }
}
