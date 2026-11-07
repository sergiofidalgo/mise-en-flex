using System;
using System.Collections.Generic;
using Game.Gameplay.Stations;
using Game.Simulation.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>
    /// The sounds chefs make themselves: footsteps, and a clack when they set something down. Staff
    /// and customers stay silent for now: they'd add a lot of noise to a busy service for little benefit.
    /// </summary>
    public sealed class PlayerSounds : MonoBehaviour
    {
        [SerializeField] private KitchenAudio _audio;

        private sealed class Chef
        {
            public PlayerInteractor Interactor;
            public Action<InteractionOutcome> OnInteracted;
            public Vector3 LastPosition;
            public readonly StepClock Steps = new StepClock();
        }

        private readonly Dictionary<PlayerInput, Chef> _chefs = new Dictionary<PlayerInput, Chef>();

        private void Update()
        {
            // Players can join at any moment, so look for new ones as we go.
            foreach (var player in PlayerInput.all)
            {
                if (!_chefs.TryGetValue(player, out var chef))
                    _chefs[player] = chef = NewChef(player);
                UpdateSteps(player, chef);
            }
        }

        private void OnDestroy()
        {
            foreach (var chef in _chefs.Values)
            {
                if (chef.Interactor != null)
                    chef.Interactor.Interacted -= chef.OnInteracted;
            }
        }

        private Chef NewChef(PlayerInput player)
        {
            var chef = new Chef { LastPosition = player.transform.position };
            chef.Steps.Stop(_audio.Sounds.StepStride * _audio.Sounds.StepFirstStride);
            if (player.TryGetComponent(out chef.Interactor))
            {
                chef.OnInteracted = outcome => HandleInteracted(outcome, player);
                chef.Interactor.Interacted += chef.OnInteracted;
            }
            return chef;
        }

        private void UpdateSteps(PlayerInput player, Chef chef)
        {
            var deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            var sounds = _audio.Sounds;
            var position = player.transform.position;
            var moved = position - chef.LastPosition;
            moved.y = 0f;
            chef.LastPosition = position;

            // Measured from where they actually got to, so pushing against a wall stays silent.
            var distance = moved.magnitude;
            if (distance / deltaTime < sounds.StepMinSpeed)
                chef.Steps.Stop(sounds.StepStride * sounds.StepFirstStride);
            else if (chef.Steps.Walk(distance, sounds.StepStride))
                _audio.Play(sounds.Step, position, SoundMath.ChefPitch(player.playerIndex, sounds.StepPitchPerChef));
        }

        private void HandleInteracted(InteractionOutcome outcome, PlayerInput player)
        {
            if (outcome == InteractionOutcome.Placed || outcome == InteractionOutcome.Combined)
                _audio.Play(_audio.Sounds.Clack, player.transform.position);
        }
    }
}
