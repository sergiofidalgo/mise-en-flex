using Game.Data;
using Game.Gameplay;
using Game.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// Adds the players' foot rings (FootRingView, decision 2026-11-11) to a scene being built:
    /// the office passes no kitchen root, so its rings always show.
    /// </summary>
    public static class FootRings
    {
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";
        private const string ShaderName = "Game/FootRing";

        public static FootRingView Add(CoopSettings settings, KitchenCompositionRoot root, LayoutEditor editor)
        {
            var view = new GameObject("FootRings").AddComponent<FootRingView>();
            var so = new SerializedObject(view);
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.FindProperty("_material").objectReferenceValue = EnsureMaterial("MAT_FootRing", CompareFunction.LessEqual);
            so.FindProperty("_cursorMaterial").objectReferenceValue = EnsureMaterial("MAT_FootRing_OnTop", CompareFunction.Always);
            so.FindProperty("_root").objectReferenceValue = root;
            so.FindProperty("_editor").objectReferenceValue = editor;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        /// <summary>The ring's material (SH_FootRing) with this depth test. Created once.</summary>
        private static Material EnsureMaterial(string name, CompareFunction depthTest)
        {
            var path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find(ShaderName));
            material.SetFloat("_ZTest", (float)depthTest);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
