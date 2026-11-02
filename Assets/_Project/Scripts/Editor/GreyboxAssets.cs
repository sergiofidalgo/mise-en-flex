using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Small asset helpers shared by the greybox builders.
    /// </summary>
    public static class GreyboxAssets
    {
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";
        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private const string ParticleShaderName = "Universal Render Pipeline/Particles/Unlit";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        /// <summary>Loads the asset at <paramref name="path"/>, creating it with defaults if missing. Never overwrites tuning.</summary>
        public static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            EnsureFolder(System.IO.Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>
        /// A flat-coloured URP Lit material named MAT_Greybox_<paramref name="name"/>. Created once;
        /// later colour edits in the Editor are kept.
        /// </summary>
        public static Material EnsureMaterial(string name, Color color)
        {
            var path = $"{MaterialsFolder}/MAT_Greybox_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            EnsureFolder(MaterialsFolder);
            material = new Material(Shader.Find(LitShaderName));
            material.SetColor(BaseColorId, color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// A soft, transparent URP particle material (MAT_Particle_<paramref name="name"/>) using Unity's
        /// built-in round particle texture. Created once.
        /// </summary>
        public static Material EnsureParticleMaterial(string name)
        {
            var path = $"{MaterialsFolder}/MAT_Particle_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            EnsureFolder(MaterialsFolder);
            material = new Material(Shader.Find(ParticleShaderName));
            var texture = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (texture != null)
                material.SetTexture(BaseMapId, texture);
            else
                Debug.LogWarning("[GreyboxAssets] Built-in particle texture not found; smoke will look square.");

            // Alpha-blended transparency, without writing depth, so puffs fade softly.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Material material, bool keepCollider)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            if (!keepCollider)
                Object.DestroyImmediate(primitive.GetComponent<Collider>());
            primitive.transform.SetParent(parent, false);
            if (material != null)
                primitive.GetComponent<Renderer>().sharedMaterial = material;
            return primitive;
        }

        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
