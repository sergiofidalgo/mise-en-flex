using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Sets the plate shader's grime (Game/Kitchen/Plate): how dirty, 0..1, and which mess. Plates
    /// shown by a station, not as items (in the sink, on the stack), carry this to look dirty: each
    /// time one is shown it gets a new mess.
    /// </summary>
    public sealed class PlateGrime : MonoBehaviour
    {
        private static readonly int DirtId = Shader.PropertyToID("_Dirt");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        [SerializeField] private Renderer _body;
        [Tooltip("How dirty it looks when shown: 1 as a diner left it.")]
        [SerializeField, Range(0f, 1f)] private float _dirt = 1f;

        private MaterialPropertyBlock _block;
        private float _seed;

        /// <summary>True if this renderer's material draws grime.</summary>
        public static bool HasGrime(Renderer renderer) =>
            renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(DirtId);

        public static void Set(MaterialPropertyBlock block, float dirt, float seed)
        {
            block.SetFloat(DirtId, dirt);
            block.SetFloat(SeedId, seed);
        }

        private void OnEnable()
        {
            _seed = Random.value;
            Apply();
        }

        /// <summary>How dirty, 0 clean to 1 as left: a plate being washed comes clean.</summary>
        public void SetDirt(float dirt)
        {
            if (Mathf.Approximately(dirt, _dirt))
                return;
            _dirt = dirt;
            Apply();
        }

        private void Apply()
        {
            if (!HasGrime(_body))
                return;
            _block ??= new MaterialPropertyBlock();
            _body.GetPropertyBlock(_block);
            Set(_block, _dirt, _seed);
            _body.SetPropertyBlock(_block);
        }
    }
}
