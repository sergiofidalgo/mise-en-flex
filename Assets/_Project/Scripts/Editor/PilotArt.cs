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

        /// <summary>
        /// A plain URP Unlit material (MAT_Unlit_<paramref name="name"/>), for things that give off
        /// their own light, such as flames; views set their colour. Created once.
        /// </summary>
        public static Material EnsureUnlitMaterial(string name, Color color)
        {
            var path = $"{MaterialsFolder}/MAT_Unlit_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor(BaseColorId, color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// A soft, additive particle material (MAT_Particle_Fire): glowing puffs and embers that
        /// brighten what's behind them, as fire does. Created once.
        /// </summary>
        public static Material EnsureFireMaterial()
        {
            const string path = MaterialsFolder + "/MAT_Particle_Fire.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(GreyboxAssets.EnsureParticleMaterial("Smoke"));
            material.SetFloat("_Blend", 2f); // additive
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>The soft, additive gas flame material (MAT_FX_Flame, Game/FX/Flame). Created once.</summary>
        public static Material EnsureFlameMaterial()
        {
            const string path = MaterialsFolder + "/MAT_FX_Flame.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var isNew = material == null;
            if (isNew)
                material = new Material(Shader.Find("Game/FX/Flame"));
            // Not too bright, so the blue stays blue over the light steel rather than washing white.
            material.SetFloat("_Intensity", 1.1f);
            material.SetFloat("_EdgeSoftness", 2f);
            if (isNew)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Running water (MAT_Water): unlit, pale blue and see-through, for the sink's stream.
        /// Created once.
        /// </summary>
        public static Material EnsureWaterMaterial()
        {
            const string path = MaterialsFolder + "/MAT_Water.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor(BaseColorId, new Color(0.72f, 0.88f, 1f, 0.6f));
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

        /// <summary>The water standing in the sink (MAT_SinkWater): a see-through blue. Created once.</summary>
        public static Material EnsureSinkWaterMaterial()
        {
            const string path = MaterialsFolder + "/MAT_SinkWater.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(EnsureWaterMaterial());
            material.SetColor(BaseColorId, new Color(0.42f, 0.66f, 0.88f, 0.42f));
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
