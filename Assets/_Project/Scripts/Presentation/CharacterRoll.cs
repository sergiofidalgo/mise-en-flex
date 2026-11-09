using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Rolls the person inside a customer's archetype as they appear (concept v9.2: "the archetype
    /// is the clothes, the person is rolled"; the user's playtest found two identical customers):
    /// skin tone, hair style and colour, the shade of their clothes, nose, glasses or a moustache,
    /// and the archetype's props. Every option is already built on the body; this shows one of
    /// each and recolours by property block. Two people in the room at once never roll exactly
    /// the same. Cosmetic only.
    /// </summary>
    public sealed class CharacterRoll : MonoBehaviour
    {
        /// <summary>One choice among parts: one option shows, or none with <see cref="NoneChance"/>.</summary>
        [Serializable]
        public struct Choice
        {
            public string Name;
            public GameObject[] Options;
            [Range(0f, 1f)] public float NoneChance;
        }

        /// <summary>One colour picked for a set of parts (all the skin, all the hair...).</summary>
        [Serializable]
        public struct Palette
        {
            public string Name;
            public Renderer[] Renderers;
            public Color[] Colours;
        }

        [Tooltip("The archetype, so only people of the same archetype count as repeats.")]
        [SerializeField] private string _archetype = "";
        [SerializeField] private Choice[] _choices = Array.Empty<Choice>();
        [SerializeField] private Palette[] _palettes = Array.Empty<Palette>();

        private const int Attempts = 24;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        // Who's in the room now, as archetype and rolled choices, so a new arrival can avoid them.
        private static readonly HashSet<int> InRoom = new HashSet<int>();
        private int _rolled;
        private bool _registered;

        private void OnEnable()
        {
            var random = new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
            for (var attempt = 0; attempt < Attempts; attempt++)
            {
                _rolled = Roll(random);
                if (!InRoom.Contains(_rolled))
                    break;
            }
            _registered = InRoom.Add(_rolled);
        }

        private void OnDisable()
        {
            if (_registered)
                InRoom.Remove(_rolled);
            _registered = false;
        }

        /// <summary>
        /// Picks and shows a person; returns a key for the combination. Public so a still picture
        /// can roll with a fixed seed outside Play mode.
        /// </summary>
        public int Roll(System.Random random)
        {
            var key = _archetype.GetHashCode();
            foreach (var choice in _choices)
            {
                var pick = choice.Options.Length == 0 || random.NextDouble() < choice.NoneChance
                    ? -1
                    : random.Next(choice.Options.Length);
                // All off, then the pick on: an option may be listed twice to weight it.
                foreach (var option in choice.Options)
                    if (option != null)
                        option.SetActive(false);
                if (pick >= 0 && choice.Options[pick] != null)
                    choice.Options[pick].SetActive(true);
                key = key * 31 + pick + 1;
            }

            var block = new MaterialPropertyBlock();
            foreach (var palette in _palettes)
            {
                if (palette.Colours.Length == 0)
                    continue;
                var pick = random.Next(palette.Colours.Length);
                key = key * 31 + pick;
                foreach (var renderer in palette.Renderers)
                {
                    if (renderer == null)
                        continue;
                    renderer.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, palette.Colours[pick]);
                    renderer.SetPropertyBlock(block);
                }
            }
            return key;
        }
    }
}
