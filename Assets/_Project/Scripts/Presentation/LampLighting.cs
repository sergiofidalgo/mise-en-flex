using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Turns lamps on for the toon shading while this scene is open (decisions 2026-11-09). The toon
    /// shaders ignore point and spot lights unless the global _GameLocalLights is above 0, so each
    /// scene says how much its lamps count: the office at night leans on them, the kitchen only
    /// warms with them. Runs in the editor too, so a scene is lit as it will be while it's looked at.
    /// </summary>
    [ExecuteAlways]
    public sealed class LampLighting : MonoBehaviour
    {
        private static readonly int LocalLights = Shader.PropertyToID("_GameLocalLights");

        [Tooltip("How strongly lamps light surfaces: 1 is as the lights are set.")]
        [SerializeField, Min(0f)] private float _localLights = 1f;

        private void OnEnable() => Shader.SetGlobalFloat(LocalLights, _localLights);

        private void OnValidate() => Shader.SetGlobalFloat(LocalLights, _localLights);

        private void OnDisable() => Shader.SetGlobalFloat(LocalLights, 0f);
    }
}
