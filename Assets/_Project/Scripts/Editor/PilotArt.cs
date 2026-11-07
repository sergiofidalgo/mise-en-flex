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

        /// <summary>
        /// A clear, glossy, transparent URP Lit material (MAT_Glass_<paramref name="name"/>), for
        /// glass you see food through. Created once.
        /// </summary>
        public static Material EnsureGlassMaterial(string name, Color tint)
        {
            var path = $"{MaterialsFolder}/MAT_Glass_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor(BaseColorId, tint);
            material.SetFloat("_Smoothness", 0.95f);
            material.SetFloat("_Surface", 1f); // transparent
            material.SetFloat("_Blend", 0f);   // alpha
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
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

    /// <summary>
    /// Import settings for models (meshes only, with Blender's normals and scale kept) and for
    /// ingredient pictures (UI sprites with transparency).
    /// </summary>
    public sealed class PilotArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(IngredientIconRenderer.IconFolder))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = true; // tests check the background is transparent
        }

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
