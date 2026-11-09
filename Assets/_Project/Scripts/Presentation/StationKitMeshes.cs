using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Simple meshes for station interfaces built in 3D (the wash ring's segments and target),
    /// made in code so their sizes follow the tuning. Flat-shaded, in a local space where y is up
    /// and angles run clockwise from +z (screen up, once the piece is turned to the camera).
    /// </summary>
    public static class StationKitMeshes
    {
        /// <summary>
        /// A block shaped like a slice of a ring: from <paramref name="inner"/> to <paramref name="outer"/>
        /// radius, between two angles (radians, clockwise from +z), <paramref name="height"/> tall,
        /// with its top edges chamfered by <paramref name="bevel"/> so the toon light catches them.
        /// </summary>
        public static Mesh RingSector(float inner, float outer, float from, float to, float height, float bevel, int steps = 6)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            bevel = Mathf.Min(bevel, (outer - inner) * 0.4f, height * 0.8f);

            // Each cross-section, inside to outside: bottom-in, top-in (lowered), bevel-in, bevel-out, top-out (lowered), bottom-out.
            Vector3 At(float radius, float y, float angle) => new Vector3(Mathf.Sin(angle) * radius, y, Mathf.Cos(angle) * radius);
            var profile = new[]
            {
                (inner, 0f), (inner, height - bevel), (inner + bevel, height), (outer - bevel, height), (outer, height - bevel), (outer, 0f),
            };

            // The faces along the ring: one quad strip per edge of the profile.
            for (var p = 0; p < profile.Length - 1; p++)
            {
                for (var s = 0; s < steps; s++)
                {
                    var a0 = Mathf.Lerp(from, to, s / (float)steps);
                    var a1 = Mathf.Lerp(from, to, (s + 1) / (float)steps);
                    var v0 = At(profile[p].Item1, profile[p].Item2, a0);
                    var v1 = At(profile[p + 1].Item1, profile[p + 1].Item2, a0);
                    var v2 = At(profile[p + 1].Item1, profile[p + 1].Item2, a1);
                    var v3 = At(profile[p].Item1, profile[p].Item2, a1);
                    Quad(vertices, triangles, v0, v1, v2, v3);
                }
            }

            // The two ends, as fans over the profile.
            foreach (var (angle, flip) in new[] { (from, true), (to, false) })
            {
                var start = vertices.Count;
                foreach (var (radius, y) in profile)
                    vertices.Add(At(radius, y, angle));
                for (var i = 1; i < profile.Length - 1; i++)
                {
                    if (flip)
                        triangles.AddRange(new[] { start, start + i + 1, start + i });
                    else
                        triangles.AddRange(new[] { start, start + i, start + i + 1 });
                }
            }

            var mesh = new Mesh { name = "RingSector" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Unshared corners, so every face is flat-shaded.
        private static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var i = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
    }
}
