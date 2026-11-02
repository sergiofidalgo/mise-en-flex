using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Local co-op tuning: how many players can join and how each one is told apart.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Coop Settings", fileName = "CoopSettings")]
    public sealed class CoopSettings : ScriptableObject
    {
        [Tooltip("Maximum local players. Designed and tested for 2; 3-4 is a stretch goal.")]
        [SerializeField, Min(1)] private int _maxPlayers = 2;

        [Tooltip("Body colour per player, in join order. Must read clearly from the overhead camera.")]
        [SerializeField] private Color[] _playerColors =
        {
            new Color(0.95f, 0.55f, 0.15f), // warm orange
            new Color(0.15f, 0.70f, 0.75f), // teal
            new Color(0.85f, 0.25f, 0.45f), // raspberry
            new Color(0.55f, 0.75f, 0.20f), // lime
        };

        public int MaxPlayers => _maxPlayers;

        public Color GetPlayerColor(int playerIndex)
        {
            if (_playerColors == null || _playerColors.Length == 0)
                return Color.white;

            return _playerColors[playerIndex % _playerColors.Length];
        }
    }
}
