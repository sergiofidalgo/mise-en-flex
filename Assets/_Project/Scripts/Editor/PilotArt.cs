using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Lets the greybox builders use a real model when it exists. They rebuild prefabs from
    /// primitives, so this is what keeps art from being overwritten by the next build; without
    /// the model, they fall back to the primitive and everything still works.
    /// </summary>
    public static class PilotArt
    {
        public const string ModelsFolder = "Assets/_Project/Art/Models";
        public const string FoodShaderName = "Game/Food/Layered";
        public const string OverlayShaderName = "Game/AlwaysVisible";

        private const string MaterialsFolder = "Assets/_Project/Art/Materials";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BandBottomId = Shader.PropertyToID("_BandBottom");
        private static readonly int BandCoreId = Shader.PropertyToID("_BandCore");
        private static readonly int BandTopId = Shader.PropertyToID("_BandTop");

        public static Mesh FindMesh(string model, string meshName) =>
            AssetDatabase.LoadAllAssetsAtPath($"{ModelsFolder}/{model}.fbx")
                .OfType<Mesh>()
                .FirstOrDefault(mesh => mesh.name == meshName);

        /// <summary>
        /// Swaps a primitive's mesh for a model's, if the model exists. Model coordinates are the
        /// parent's own (origin at its base, centred), so the primitive's placement is cleared.
        /// </summary>
        public static bool TryUseMesh(GameObject primitive, string model, string meshName)
        {
            var mesh = FindMesh(model, meshName);
            if (mesh == null)
                return false;

            primitive.GetComponent<MeshFilter>().sharedMesh = mesh;
            primitive.transform.localPosition = Vector3.zero;
            primitive.transform.localRotation = Quaternion.identity;
            primitive.transform.localScale = Vector3.one;
            return true;
        }

        /// <summary>A material that shows through anything in front of it (MAT_Overlay_<paramref name="name"/>). Created once.</summary>
        public static Material EnsureOverlayMaterial(string name, Color color)
        {
            var path = $"{MaterialsFolder}/MAT_Overlay_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find(OverlayShaderName));
            material.SetColor(BaseColorId, color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>The layered food material (MAT_Food_<paramref name="name"/>). Created once.</summary>
        public static Material EnsureLayeredFoodMaterial(string name, Color rawColor)
        {
            var path = $"{MaterialsFolder}/MAT_Food_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find(FoodShaderName));
            material.SetColor(BaseColorId, rawColor);
            material.SetColor(BandBottomId, rawColor);
            material.SetColor(BandCoreId, rawColor);
            material.SetColor(BandTopId, rawColor);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }

    /// <summary>Import settings for models: meshes only, with Blender's normals and scale kept.</summary>
    public sealed class PilotArtImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(PilotArt.ModelsFolder))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.globalScale = 1f;
            importer.useFileScale = true;
        }
    }
}
