using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Rolls the person inside a customer's archetype as they appear (concept v9.2: "the archetype
    /// is the clothes, the person is rolled"; the user's playtest found two identical customers):
    /// skin tone, hair style and colour, the shade and pattern of their clothes, nose, glasses or
    /// a moustache, a hat from the archetype's few, and the archetype's props. Every option is already built on the body; this shows one of
    /// each and recolours by property block. Two people in the room at once never roll exactly
    /// the same. Cosmetic only.
    /// </summary>
    public sealed class CharacterRoll : MonoBehaviour
    {
        /// <summary>One option: the parts that show together (a jacket is its front and both sleeves).</summary>
        [Serializable]
        public struct Option
        {
            public GameObject[] Parts;
        }

        /// <summary>One choice among options: one shows, or none with <see cref="NoneChance"/>.</summary>
        [Serializable]
        public struct Choice
        {
            public string Name;
            public Option[] Options;
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

        /// <summary>A cloth pattern for a set of parts (their clothes), printed by the toon shader.</summary>
        [Serializable]
        public struct Pattern
        {
            public Renderer[] Renderers;
            [Tooltip("Game/Toon's _Pattern: 0 plain, 1 stripes, 2 plaid, 3 dots, 4 pinstripes, 5 gingham. List one twice to make it likelier.")]
            public int[] Kinds;
            public Color[] Colours;
            [Min(0.1f)] public float Scale;
        }

        [Tooltip("The archetype, so only people of the same archetype count as repeats.")]
        [SerializeField] private string _archetype = "";
        [SerializeField] private Choice[] _choices = Array.Empty<Choice>();
        [SerializeField] private Palette[] _palettes = Array.Empty<Palette>();
        [SerializeField] private Pattern[] _patterns = Array.Empty<Pattern>();
        [Tooltip("Hats (options of a choice above) that cover the top of the head.")]
        [SerializeField] private GameObject[] _coveringHats = Array.Empty<GameObject>();
        [Tooltip("The hair that only shows on a bare head: hidden when one of those hats is rolled, or it would poke through.")]
        [SerializeField] private GameObject[] _bareHair = Array.Empty<GameObject>();

        private const int Attempts = 24;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int PatternId = Shader.PropertyToID("_Pattern");
        private static readonly int PatternColorId = Shader.PropertyToID("_PatternColor");
        private static readonly int PatternScaleId = Shader.PropertyToID("_PatternScale");
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

        private static void Show(Option option, bool shown)
        {
            if (option.Parts == null)
                return;
            foreach (var part in option.Parts)
                if (part != null)
                    part.SetActive(shown);
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
                    Show(option, false);
                if (pick >= 0)
                    Show(choice.Options[pick], true);
                key = key * 31 + pick + 1;
            }

            // The under-hat rule: a hat that covers the head leaves only the hair below it.
            var covered = false;
            foreach (var hat in _coveringHats)
                covered |= hat != null && hat.activeSelf;
            foreach (var top in _bareHair)
                if (top != null)
                    top.SetActive(!covered);

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

            foreach (var pattern in _patterns)
            {
                if (pattern.Kinds.Length == 0)
                    continue;
                var kind = pattern.Kinds[random.Next(pattern.Kinds.Length)];
                var colour = pattern.Colours.Length > 0 ? random.Next(pattern.Colours.Length) : 0;
                key = key * 31 + kind;
                key = key * 31 + (kind == 0 ? 0 : colour);
                foreach (var renderer in pattern.Renderers)
                {
                    if (renderer == null)
                        continue;
                    renderer.GetPropertyBlock(block);
                    block.SetFloat(PatternId, kind);
                    if (pattern.Colours.Length > 0)
                        block.SetColor(PatternColorId, pattern.Colours[colour]);
                    block.SetFloat(PatternScaleId, pattern.Scale);
                    renderer.SetPropertyBlock(block);
                }
            }
            return key;
        }
    }
}
