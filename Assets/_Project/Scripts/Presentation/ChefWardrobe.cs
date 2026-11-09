using System;
using Game.Gameplay.Saving;
using Game.Simulation.Characters;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>
    /// Everything a chef can wear, built on the body and shown by look (the tailor, decision
    /// 2026-11-09): a hat, a face with its hair, a skin tone, an accessory and an outfit. A player's
    /// chef wears their slot's look from the profile as they join, and changes as soon as the tailor
    /// changes it. A hat that covers the head hides the top of the face's hair (the under-hat rule).
    /// </summary>
    public sealed class ChefWardrobe : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string Id;
            [Tooltip("Shown while this is worn.")]
            public GameObject[] Parts;
            [Tooltip("Faces: the top of the hair, shown only under no hat or one that doesn't cover the head.")]
            public GameObject[] Bare;
            [Tooltip("Hats: covers the head, so a face's top hair hides.")]
            public bool Covers;
            [Tooltip("Skins: the tone.")]
            public Color Colour;
        }

        [SerializeField] private Entry[] _hats = Array.Empty<Entry>();
        [SerializeField] private Entry[] _faces = Array.Empty<Entry>();
        [SerializeField] private Entry[] _skins = Array.Empty<Entry>();
        [SerializeField] private Entry[] _accessories = Array.Empty<Entry>();
        [SerializeField] private Entry[] _outfits = Array.Empty<Entry>();
        [Tooltip("Everything in the skin's tone: head, noses, hands, bare arms and shoulders.")]
        [SerializeField] private Renderer[] _skin = Array.Empty<Renderer>();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private PlayerInput _player;
        private MaterialPropertyBlock _block;

        /// <summary>What the chef shows now.</summary>
        public ChefLook Look { get; private set; } = new ChefLook();

        private void Start()
        {
            _player = GetComponentInParent<PlayerInput>();
            if (_player != null)
            {
                Show(ChefProfileFile.Current.Look(_player.playerIndex));
                ChefProfileFile.LookChanged += HandleLookChanged;
            }
            else
                Show(Look);
        }

        private void OnDestroy() => ChefProfileFile.LookChanged -= HandleLookChanged;

        private void HandleLookChanged(int slot)
        {
            if (_player != null && slot == _player.playerIndex)
                Show(ChefProfileFile.Current.Look(slot));
        }

        /// <summary>Shows <paramref name="look"/>; anything it names that isn't built falls back to the first.</summary>
        public void Show(ChefLook look)
        {
            Look = look.Copy();
            var hat = Pick(_hats, look.Hat);
            var face = Pick(_faces, look.Face);
            Pick(_accessories, look.Accessory);
            Pick(_outfits, look.Outfit);
            var covered = hat >= 0 && _hats[hat].Covers;
            for (var i = 0; i < _faces.Length; i++)
                SetActive(_faces[i].Bare, i == face && !covered);

            var skin = Find(_skins, look.Skin);
            if (skin < 0 && _skins.Length > 0)
                skin = 0;
            if (skin >= 0)
            {
                _block ??= new MaterialPropertyBlock();
                foreach (var renderer in _skin)
                {
                    if (renderer == null)
                        continue;
                    renderer.GetPropertyBlock(_block);
                    _block.SetColor(BaseColorId, _skins[skin].Colour);
                    renderer.SetPropertyBlock(_block);
                }
            }
        }

        // Shows the entry with this id (or the first) and hides the rest; returns its index.
        private static int Pick(Entry[] entries, string id)
        {
            var pick = Find(entries, id);
            if (pick < 0 && entries.Length > 0)
                pick = 0;
            for (var i = 0; i < entries.Length; i++)
                SetActive(entries[i].Parts, false);
            if (pick >= 0)
                SetActive(entries[pick].Parts, true);
            return pick;
        }

        private static int Find(Entry[] entries, string id)
        {
            for (var i = 0; i < entries.Length; i++)
                if (entries[i].Id == id)
                    return i;
            return -1;
        }

        private static void SetActive(GameObject[] parts, bool shown)
        {
            if (parts == null)
                return;
            foreach (var part in parts)
                if (part != null && part.activeSelf != shown)
                    part.SetActive(shown);
        }
    }
}
