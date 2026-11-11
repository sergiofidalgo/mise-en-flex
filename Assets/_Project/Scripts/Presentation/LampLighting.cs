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
        private static readonly int LampBands = Shader.PropertyToID("_GameLampBands");

        [Tooltip("How strongly lamps light surfaces: 1 is as the lights are set.")]
        [SerializeField, Min(0f)] private float _localLights = 1f;
        [Tooltip("0: lamps fall off smoothly (the user's call, 2026-11-09). Above 0: in that many flat steps, as the sun's toon bands (a trial, 2026-11-11).")]
        [SerializeField, Range(0f, 8f)] private float _lampBands;

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void OnDisable()
        {
            Shader.SetGlobalFloat(LocalLights, 0f);
            Shader.SetGlobalFloat(LampBands, 0f);
        }

        private void Apply()
        {
            Shader.SetGlobalFloat(LocalLights, _localLights);
            Shader.SetGlobalFloat(LampBands, Mathf.Round(_lampBands));
        }
    }
}
