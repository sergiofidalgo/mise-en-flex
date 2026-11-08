namespace Game.Gameplay.Stations
{
    /// <summary>
    /// Something that sits on a counter and takes it over: a cutting board or a knife rack
    /// (decision 2026-11-07). It's moved only in management mode, from one free counter to another,
    /// and goes wherever its counter goes. It has no floor of its own.
    /// </summary>
    public abstract class CounterTopStation : Interactable
    {
        /// <summary>The counter it's on.</summary>
        public CounterStation OnCounter { get; private set; }

        public override void Bind(KitchenServices services)
        {
            // Placed in the kitchen as it was built: it starts on the counter it stands on.
            if (OnCounter == null && transform.parent != null && transform.parent.TryGetComponent(out CounterStation counter))
                PutOn(counter);
        }

        /// <summary>Moves it onto <paramref name="counter"/>, which has to have room for it.</summary>
        public void PutOn(CounterStation counter)
        {
            if (OnCounter != null)
                OnCounter.Detach(this);
            OnCounter = counter;
            transform.SetParent(counter.transform, false);
            transform.localPosition = UnityEngine.Vector3.zero;
            transform.localRotation = UnityEngine.Quaternion.identity;
            counter.Attach(this);
        }

        /// <summary>Sold: its counter is free again.</summary>
        public override void Retire(KitchenServices services)
        {
            if (OnCounter != null)
                OnCounter.Detach(this);
            OnCounter = null;
        }

        /// <summary>A counter it could go on: nothing lying there, and no other of its kind (a board and a rack share).</summary>
        public bool CanGoOn(CounterStation counter) => counter != null && counter.HasRoomFor(this);
    }
}
