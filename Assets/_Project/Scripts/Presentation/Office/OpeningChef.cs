using Game.Data;
using Game.Gameplay.Saving;
using UnityEngine;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The chef in the opening (the user's notes, 2026-11-10): already in the tower's lift at the
    /// street while the menu is up, wearing player 1's look from the tailor and their colour, and
    /// riding up beside the camera the moment a campaign is chosen. Upstairs, player 1 comes out of
    /// the floor's elevator: the same chef. (They first walked in from the kerb, but that was hard
    /// to notice and held the climb back.)
    /// </summary>
    public sealed class OpeningChef : MonoBehaviour
    {
        [SerializeField] private ChefWardrobe _wardrobe;
        [Tooltip("The parts in the player's colour (toque band, apron, neckerchief).")]
        [SerializeField] private Renderer[] _tinted = System.Array.Empty<Renderer>();
        [SerializeField] private CoopSettings _coop;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Start()
        {
            // Player 1's own chef: their look from the tailor, their colour.
            if (_wardrobe != null)
                _wardrobe.Show(ChefProfileFile.Current.Look(0));
            if (_coop == null)
                return;
            var block = new MaterialPropertyBlock();
            foreach (var part in _tinted)
            {
                if (part == null)
                    continue;
                part.GetPropertyBlock(block);
                block.SetColor(BaseColorId, _coop.GetPlayerColor(0));
                part.SetPropertyBlock(block);
            }
        }
    }
}
