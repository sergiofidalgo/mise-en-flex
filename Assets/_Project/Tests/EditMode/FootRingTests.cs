using Game.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>The players' foot rings (decision 2026-11-11): where each one goes, and its shape.</summary>
    public class FootRingTests
    {
        [Test]
        public void ShowsAtTheChefInTheOfficeAndInPrep() =>
            Assert.That(FootRingView.PlaceFor(showing: true, rearranging: false), Is.EqualTo(FootRingPlace.AtChef));

        [Test]
        public void MovesToTheCursorWhileRearranging() =>
            Assert.That(FootRingView.PlaceFor(showing: true, rearranging: true), Is.EqualTo(FootRingPlace.AtCursor));

        [Test]
        public void HiddenInServiceAndReview()
        {
            Assert.That(FootRingView.PlaceFor(showing: false, rearranging: false), Is.EqualTo(FootRingPlace.Hidden));
            Assert.That(FootRingView.PlaceFor(showing: false, rearranging: true), Is.EqualTo(FootRingPlace.Hidden));
        }

        [Test]
        public void RingIsFlatFacesUpAndSpansItsRadii()
        {
            var mesh = FootRingView.BuildRing(0.4f, 0.5f, 32);
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(64));
                Assert.That(mesh.bounds.size.y, Is.EqualTo(0f).Within(1e-6f));
                Assert.That(mesh.bounds.extents.x, Is.EqualTo(0.5f).Within(1e-4f));
                foreach (var vertex in mesh.vertices)
                    Assert.That(new Vector2(vertex.x, vertex.z).magnitude, Is.EqualTo(0.4f).Within(1e-4f).Or.EqualTo(0.5f).Within(1e-4f));

                // Every triangle faces up (Unity's front faces wind clockwise seen from above).
                var v = mesh.vertices;
                var t = mesh.triangles;
                for (var i = 0; i < t.Length; i += 3)
                    Assert.That(Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).y, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
