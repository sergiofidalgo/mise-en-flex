using Game.Gameplay.Office;
using Game.Simulation.Saving;
using UnityEngine;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The boss's floor: the city miniature's cursor, presses and refusals, a fanfare for a restaurant
    /// bought, and the accountant's and tailor's choices. The elevator keeps its own sounds
    /// (ElevatorView). Also what the main menu plays through.
    /// </summary>
    public sealed class OfficeAudio : SfxPlayer
    {
        private MiniatureTable _miniature;
        private OfficeCampaign _campaign;
        private AccountantDesk _accountant;
        private TailorPlinth _tailor;

        private void Start()
        {
            _miniature = FindFirstObjectByType<MiniatureTable>();
            if (_miniature != null)
            {
                _miniature.Moved += HandleMoved;
                _miniature.Pressed += HandlePressed;
                _miniature.Refused += HandleRefused;
                _miniature.Leaving += HandleLeaving;
            }
            _campaign = FindFirstObjectByType<OfficeCampaign>();
            if (_campaign != null)
                _campaign.Bought += HandleBought;
            _accountant = FindFirstObjectByType<AccountantDesk>();
            if (_accountant != null)
            {
                _accountant.Stepped += HandleStepped;
                _accountant.Borrowed += HandleBorrowed;
            }
            _tailor = FindFirstObjectByType<TailorPlinth>();
            if (_tailor != null)
                _tailor.Stepped += HandleStepped;
        }

        private void OnDestroy()
        {
            if (_miniature != null)
            {
                _miniature.Moved -= HandleMoved;
                _miniature.Pressed -= HandlePressed;
                _miniature.Refused -= HandleRefused;
                _miniature.Leaving -= HandleLeaving;
            }
            if (_campaign != null)
                _campaign.Bought -= HandleBought;
            if (_accountant != null)
            {
                _accountant.Stepped -= HandleStepped;
                _accountant.Borrowed -= HandleBorrowed;
            }
            if (_tailor != null)
                _tailor.Stepped -= HandleStepped;
        }

        private void HandleMoved() => PlayUi(Sounds.UiMove);

        private void HandlePressed() => PlayUi(Sounds.UiConfirm);

        private void HandleRefused() => PlayUi(Sounds.Deny);

        private void HandleLeaving() => PlayUi(Sounds.UiBack);

        // Zero means it couldn't move (already at the end): that's a refusal, not a step.
        private void HandleStepped(int direction) => PlayUi(direction != 0 ? Sounds.UiMove : Sounds.Deny);

        private void HandleBorrowed(float amount) => PlayCentred(Sounds.Till);

        private void HandleBought(RestaurantSave restaurant) => PlayCentred(Sounds.Fanfare);
    }
}
