using System;

namespace Game.Simulation.Office
{
    /// <summary>Timings for the office elevator's doors, in seconds.</summary>
    public readonly struct ElevatorSettings
    {
        public ElevatorSettings(float ringToOpen, float opening, float holdOpen, float closing)
        {
            RingToOpen = Math.Max(0f, ringToOpen);
            Opening = Math.Max(0.01f, opening);
            HoldOpen = Math.Max(0f, holdOpen);
            Closing = Math.Max(0.01f, closing);
        }

        /// <summary>From the bell to the doors starting to part: the car settling at the floor.</summary>
        public float RingToOpen { get; }
        /// <summary>How long the doors take to open fully.</summary>
        public float Opening { get; }
        /// <summary>How long they stay open after the last arrival, once nobody's in the car or doorway.</summary>
        public float HoldOpen { get; }
        /// <summary>How long they take to close.</summary>
        public float Closing { get; }
    }

    /// <summary>
    /// The office elevator's doors (decision 2026-11-09): every player joins through it, and each
    /// join rings the bell as if the car had just reached the floor, then the doors open. A join
    /// while they're open rings again and keeps them open; a join while they're closing rings and
    /// reopens them. Like a real lift's sensor, anyone in the car or the doorway holds them open.
    /// </summary>
    public sealed class ElevatorDoors
    {
        public enum DoorState { Closed, Arriving, Opening, Open, Closing }

        private readonly ElevatorSettings _settings;
        private float _timer;

        public ElevatorDoors(ElevatorSettings settings)
        {
            _settings = settings;
        }

        /// <summary>The bell rang: a car arrived.</summary>
        public event Action Rang;
        /// <summary>The doors started to open.</summary>
        public event Action StartedOpening;
        /// <summary>The doors started to close.</summary>
        public event Action StartedClosing;

        public DoorState State { get; private set; } = DoorState.Closed;

        /// <summary>0 shut, 1 fully open.</summary>
        public float Openness { get; private set; }

        /// <summary>Someone's in the car or the doorway: the doors won't close on them.</summary>
        public bool Occupied { get; set; }

        /// <summary>A player joined: the car arrives with them.</summary>
        public void Arrive()
        {
            Rang?.Invoke();
            switch (State)
            {
                case DoorState.Closed:
                    State = DoorState.Arriving;
                    _timer = _settings.RingToOpen;
                    break;
                case DoorState.Closing:
                    StartOpening();
                    break;
                case DoorState.Open:
                    _timer = _settings.HoldOpen;
                    break;
                // Arriving or Opening: already on their way open, and the hold starts once open.
            }
        }

        public void Tick(float deltaTime)
        {
            switch (State)
            {
                case DoorState.Arriving:
                    _timer -= deltaTime;
                    if (_timer <= 0f)
                        StartOpening();
                    break;
                case DoorState.Opening:
                    Openness = Math.Min(1f, Openness + deltaTime / _settings.Opening);
                    if (Openness >= 1f)
                    {
                        State = DoorState.Open;
                        _timer = _settings.HoldOpen;
                    }
                    break;
                case DoorState.Open:
                    // The hold only counts down once the car and doorway are clear, so a chef who
                    // lingers inside is never shut in.
                    if (Occupied)
                        _timer = _settings.HoldOpen;
                    else
                        _timer -= deltaTime;
                    if (_timer <= 0f)
                    {
                        State = DoorState.Closing;
                        StartedClosing?.Invoke();
                    }
                    break;
                case DoorState.Closing:
                    if (Occupied)
                    {
                        StartOpening();
                        break;
                    }
                    Openness = Math.Max(0f, Openness - deltaTime / _settings.Closing);
                    if (Openness <= 0f)
                        State = DoorState.Closed;
                    break;
            }
        }

        private void StartOpening()
        {
            State = DoorState.Opening;
            StartedOpening?.Invoke();
        }
    }
}
