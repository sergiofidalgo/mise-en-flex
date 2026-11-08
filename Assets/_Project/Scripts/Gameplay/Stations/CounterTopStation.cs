namespace Game.Gameplay.Stations
{
    /// <summary>
    /// Something that sits on a counter and takes it over: a cutting board (decision
    /// 2026-11-07). A knife rack used to be one too; it's now its own counter (decision 2026-11-08). It's moved only in management mode, from one free counter to another,
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

        /// <summary>Moves it onto <paramref name="counter"/>, which has to be free.</summary>
        public void PutOn(CounterStation counter)
        {
            if (OnCounter != null)
                OnCounter.SetFixture(null);
            OnCounter = counter;
            counter.SetFixture(this);
            transform.SetParent(counter.transform, false);
            transform.localPosition = UnityEngine.Vector3.zero;
            transform.localRotation = UnityEngine.Quaternion.identity;
        }

        /// <summary>Sold: its counter is free again.</summary>
        public override void Retire(KitchenServices services)
        {
            if (OnCounter != null)
                OnCounter.SetFixture(null);
            OnCounter = null;
        }

        /// <summary>A counter it could go on: one with nothing on it.</summary>
        public static bool CanGoOn(CounterStation counter) => counter != null && counter.IsClear;
    }
}
