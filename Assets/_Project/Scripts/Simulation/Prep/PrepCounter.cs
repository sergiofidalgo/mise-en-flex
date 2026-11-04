using System;
using Game.Simulation.Cooking;
using Game.Simulation.Items;

namespace Game.Simulation.Prep
{
    /// <summary>
    /// The cutting board (decisions 2026-11-02, 2026-11-04). A whole ingredient goes on it and is
    /// chopped with the knife by timing presses to the cutting dial. The cut stays on the board and
    /// blocks the next one until someone moves it, so where it goes is the player's to plan.
    /// </summary>
    public sealed class PrepCounter : IItemContainer, IItemHolder
    {
        private readonly Func<Ingredient, Ingredient> _cut;
        private readonly ChopPacing _pacing;
        private readonly float _sweepDegrees;

        // Default width of the cutting dial's arc, in degrees.
        public const float DefaultSweepDegrees = 170f;

        /// <param name="cut">Turns a finished whole ingredient into its cut form (via the cooking simulation).</param>
        /// <param name="pacing">How each cut's grade changes the dial's speed.</param>
        /// <param name="sweepDegrees">Width of the cutting dial's arc.</param>
        public PrepCounter(Func<Ingredient, Ingredient> cut, ChopPacing? pacing = null, float sweepDegrees = DefaultSweepDegrees)
        {
            _cut = cut;
            _pacing = pacing ?? ChopPacing.Default;
            _sweepDegrees = sweepDegrees;
        }

        /// <summary>What's on the board, or null.</summary>
        public Item Current { get; private set; }

        /// <summary>The cutting dial for what's on the board, or null if there's nothing to cut.</summary>
        public ChopDial Dial { get; private set; }

        /// <summary>Chops done on the item on the board.</summary>
        public int Cuts { get; private set; }

        public bool CanChop => Current is Ingredient ingredient && IsCuttable(ingredient);

        public int CutsNeeded => (Current as Ingredient)?.Properties.CutsNeeded ?? 0;

        /// <summary>Raised when a cut finishes, with the cut product.</summary>
        public event Action<Ingredient> CutFinished;

        public static bool IsCuttable(Ingredient ingredient) =>
            !string.IsNullOrEmpty(ingredient.Properties.CutIntoTypeId) && ingredient.Properties.CutsNeeded > 0;

        /// <summary>One knife chop at <paramref name="realTime"/> (seconds). Null if there's nothing to cut.</summary>
        public ChopGrade? Chop(double realTime)
        {
            if (!CanChop)
                return null;

            var whole = (Ingredient)Current;
            var grade = Dial.Hit(realTime);
            Cuts++;

            if (Cuts >= whole.Properties.CutsNeeded)
            {
                var evenness = Dial.Evenness;
                var product = _cut(whole);
                product.CutEvenness = evenness;
                ClearBoard();
                Current = product;
                CutFinished?.Invoke(product);
            }

            return grade;
        }

        public bool CanTake => Current != null;

        /// <summary>Only whole ingredients to cut go on the board, and only when it's clear.</summary>
        public bool CanAccept(Item item) => Current == null && item is Ingredient ingredient && IsCuttable(ingredient);

        public Item Take()
        {
            if (Current == null)
                throw new InvalidOperationException("Nothing on the board.");
            var item = Current;
            ClearBoard();
            return item;
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("The board can't take this.");

            var ingredient = (Ingredient)item;
            Current = ingredient;
            Cuts = 0;
            var p = ingredient.Properties;
            Dial = new ChopDial(p.ChopSecondsPerSweep, _sweepDegrees, p.SweetSpotPosition, p.SweetSpotGoodHalfWidth, p.SweetSpotOkHalfWidth, _pacing);
        }

        private void ClearBoard()
        {
            Current = null;
            Cuts = 0;
            Dial = null;
        }
    }
}
