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
            Assert.That(body.size.x, Is.EqualTo(0.32f).Within(Tolerance));
            Assert.That(body.size.z, Is.EqualTo(0.32f).Within(Tolerance));
            Assert.That(body.min.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(body.max.y, Is.LessThanOrEqualTo(0.035f), "a thick, cartoony plate, but one a pile's step high");

            var rim = Model("SM_Plate", "PlateRim");
            Assert.That(rim.size.x, Is.LessThanOrEqualTo(body.size.x + Tolerance), "The rim stays on the plate.");
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
            Assert.That(rack.size.z, Is.GreaterThanOrEqualTo(5 * 0.055f), "Room for six plates, one behind the other.");
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
            Assert.That(board.size.x, Is.EqualTo(0.34f).Within(Tolerance));
            Assert.That(board.size.z, Is.EqualTo(0.28f).Within(Tolerance));
            Assert.That(board.size.y, Is.EqualTo(0.035f).Within(Tolerance));
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
