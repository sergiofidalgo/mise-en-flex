using UnityEngine;

namespace Game.Presentation.Office
{
    /// <summary>
    /// Turns lamps on for the toon shading while this scene is open (decision 2026-11-09, the office
    /// at night). The toon shaders ignore point and spot lights unless the global _GameLocalLights is
    /// above 0, so the kitchen, which never sets it, looks exactly as before. Runs in the editor too,
    /// so the scene is lit as it will be while it's being looked at.
    /// </summary>
    [ExecuteAlways]
    public sealed class OfficeLighting : MonoBehaviour
    {
        private static readonly int LocalLights = Shader.PropertyToID("_GameLocalLights");

        [Tooltip("How strongly lamps light surfaces: 1 is as the lights are set.")]
        [SerializeField, Min(0f)] private float _localLights = 1f;

        private void OnEnable() => Shader.SetGlobalFloat(LocalLights, _localLights);

        private void OnValidate() => Shader.SetGlobalFloat(LocalLights, _localLights);

        private void OnDisable() => Shader.SetGlobalFloat(LocalLights, 0f);
    }
}
