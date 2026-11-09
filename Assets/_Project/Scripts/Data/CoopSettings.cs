using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Local co-op tuning: how many players can join and how each one is told apart.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Coop Settings", fileName = "CoopSettings")]
    public sealed class CoopSettings : ScriptableObject
    {
        [Tooltip("Maximum local players: up to four (decision 2026-11-09).")]
        [SerializeField, Min(1)] private int _maxPlayers = 4;

        // Told apart by lightness as well as hue, so they stay apart with colour blindness: orange
        // and yellow differ in lightness, teal and violet too, and each warm one sits opposite a
        // cool one. Checked by simulating protanopia, deuteranopia and tritanopia (Machado 2009):
        // the closest pair is orange and yellow, about 22 CIELAB units apart for a deuteranope.
        // The old raspberry and lime fell to 9 (orange and lime, nearly the same to a deuteranope).
        [Tooltip("Body colour per player, in join order. Must read clearly from the overhead camera and with colour blindness.")]
        [SerializeField] private Color[] _playerColors =
        {
            new Color(0.95f, 0.55f, 0.15f), // warm orange
            new Color(0.15f, 0.70f, 0.75f), // teal
            new Color(1.00f, 0.92f, 0.30f), // yellow
            new Color(0.62f, 0.40f, 0.90f), // violet
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
