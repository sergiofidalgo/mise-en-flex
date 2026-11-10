using Game.Gameplay.Office;
using Game.Gameplay.Saving;
using Game.Presentation.Hud;
using UnityEngine;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The money card's balance in the office: the chosen campaign's, read again only when the
    /// accountant lends or the miniature sells (OfficeCampaign.BalanceChanged), or a different
    /// campaign is chosen. Nothing shows on the street menu before one is. Hidden while the city
    /// miniature's panel, down the right edge, is open.
    /// </summary>
    public sealed class OfficeMoneySource : MonoBehaviour, IMoneySource
    {
        [SerializeField] private OfficeCampaign _office;
        [SerializeField] private MiniatureTable _table;

        private bool _dirty = true;
        private float _balance;
        private int _slot = -1;

        private void OnEnable()
        {
            if (_office != null)
                _office.BalanceChanged += HandleBalanceChanged;
        }

        private void OnDisable()
        {
            if (_office != null)
                _office.BalanceChanged -= HandleBalanceChanged;
        }

        private void HandleBalanceChanged() => _dirty = true;

        public float? Read(out bool reset)
        {
            reset = false;
            if (_office == null || !CampaignLaunch.Decided)
                return null;
            if (_slot != CampaignLaunch.Slot)
            {
                _slot = CampaignLaunch.Slot;
                _dirty = true;
                reset = true;
            }
            if (_dirty)
            {
                _balance = _office.Campaign.Balance;
                _dirty = false;
            }
            return _balance;
        }

        public bool Covered => _table != null && _table.Current != MiniatureTable.Step.Closed;
    }
}
