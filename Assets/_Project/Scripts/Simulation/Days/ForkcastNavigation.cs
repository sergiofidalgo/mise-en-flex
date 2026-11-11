using System;
using System.Collections.Generic;

namespace Game.Simulation.Days
{
    /// <summary>A D-pad step: one of the four directions, or none.</summary>
    public enum NavDirection
    {
        None,
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>
    /// Turns each player's D-pad into steps (playtest round, 2026-11-11): a fresh press steps at once,
    /// holding repeats, and a change of direction steps at once. Every player has their own timing.
    /// Before, the dashboard shared one timer, so an idle second player reset it every frame and a
    /// held press from the first stepped every frame, racing down the list.
    /// </summary>
    public sealed class NavRepeat
    {
        private readonly Dictionary<int, (NavDirection Direction, float Next)> _held = new Dictionary<int, (NavDirection, float)>();

        /// <summary>How far the D-pad's value goes before it counts (0..1).</summary>
        public float Threshold { get; set; } = 0.5f;

        /// <summary>Seconds a press is held before it starts repeating.</summary>
        public float FirstDelay { get; set; } = 0.35f;

        /// <summary>Seconds between repeats after that.</summary>
        public float Interval { get; set; } = 0.15f;

        /// <summary>The direction a value points, by its larger axis; None under the threshold.</summary>
        public NavDirection DirectionOf(float x, float y)
        {
            if (x * x + y * y < Threshold * Threshold)
                return NavDirection.None;
            if (Math.Abs(y) >= Math.Abs(x))
                return y > 0f ? NavDirection.Up : NavDirection.Down;
            return x > 0f ? NavDirection.Right : NavDirection.Left;
        }

        /// <summary>This player's step this frame, given their D-pad and the time now (real seconds).</summary>
        public NavDirection Update(int player, float x, float y, float now)
        {
            var direction = DirectionOf(x, y);
            if (direction == NavDirection.None)
            {
                _held.Remove(player);
                return NavDirection.None;
            }
            if (!_held.TryGetValue(player, out var held) || held.Direction != direction)
            {
                _held[player] = (direction, now + FirstDelay);
                return direction;
            }
            if (now < held.Next)
                return NavDirection.None;
            _held[player] = (direction, Math.Max(held.Next + Interval, now - Interval * 0.5f));
            return direction;
        }

        /// <summary>Forgets every held press, e.g. as a tab changes.</summary>
        public void Clear() => _held.Clear();
    }

    /// <summary>What a D-pad step did on a <see cref="ChipsOverList"/>.</summary>
    public enum NavOutcome
    {
        /// <summary>Nowhere to go that way: nothing changes.</summary>
        Nothing,
        /// <summary>The focus moved within the list, or between the chips and the list.</summary>
        Moved,
        /// <summary>The focus moved to another chip, which is also choosing it.</summary>
        ChipChanged,
        /// <summary>Left on a list item: the tab may adjust it (a price down).</summary>
        AdjustDown,
        /// <summary>Right on a list item: the tab may adjust it (a price up).</summary>
        AdjustUp,
    }

    /// <summary>
    /// The focus on a forkcast tab with a row of chips over a list (playtest round, 2026-11-11): the
    /// Overview's filters over its reviews, the Restaurant tab's opening times over its recipes; a
    /// tab with no chips is just the list (Staff). Every direction has one explicit neighbour:
    /// <list type="bullet">
    /// <item>On the chips, left and right go to the next chip (no wrapping), down to the list's top.</item>
    /// <item>In the list, up and down go to the next item; up from the top rises to the chips (to the
    /// chosen one, as that's the one in force). Left and right stay put and adjust the item.</item>
    /// </list>
    /// </summary>
    public sealed class ChipsOverList
    {
        /// <summary>Whether the focus is on the chip row rather than the list.</summary>
        public bool OnChips { get; private set; }

        /// <summary>The chosen chip (the filter or opening time in force), whether or not it has the focus.</summary>
        public int Chip { get; private set; }

        /// <summary>The focused list item; meaningful while <see cref="OnChips"/> is false.</summary>
        public int Item { get; private set; }

        /// <summary>Starts over at the list's top with <paramref name="chip"/> chosen.</summary>
        public void Reset(int chip = 0, bool onChips = false)
        {
            Chip = Math.Max(0, chip);
            Item = 0;
            OnChips = onChips;
        }

        /// <summary>Sets the focus directly (captures, and a list that shrank under it).</summary>
        public void Set(bool onChips, int chip, int item)
        {
            OnChips = onChips;
            Chip = Math.Max(0, chip);
            Item = Math.Max(0, item);
        }

        /// <summary>Keeps the item inside a list of <paramref name="items"/>, e.g. after one is removed.</summary>
        public void Clamp(int chips, int items)
        {
            Chip = chips <= 0 ? 0 : Math.Clamp(Chip, 0, chips - 1);
            Item = items <= 0 ? 0 : Math.Clamp(Item, 0, items - 1);
            if (chips <= 0)
                OnChips = false;
        }

        /// <summary>One D-pad step over <paramref name="chips"/> chips and <paramref name="items"/> items.</summary>
        public NavOutcome Move(NavDirection direction, int chips, int items)
        {
            Clamp(chips, items);
            if (direction == NavDirection.None)
                return NavOutcome.Nothing;

            if (OnChips)
            {
                switch (direction)
                {
                    case NavDirection.Left when Chip > 0:
                        Chip--;
                        return NavOutcome.ChipChanged;
                    case NavDirection.Right when Chip < chips - 1:
                        Chip++;
                        return NavOutcome.ChipChanged;
                    case NavDirection.Down when items > 0:
                        OnChips = false;
                        Item = 0;
                        return NavOutcome.Moved;
                }
                return NavOutcome.Nothing;
            }

            switch (direction)
            {
                case NavDirection.Up when Item > 0:
                    Item--;
                    return NavOutcome.Moved;
                case NavDirection.Up when chips > 0:
                    OnChips = true;
                    return NavOutcome.Moved;
                case NavDirection.Down when Item < items - 1:
                    Item++;
                    return NavOutcome.Moved;
                case NavDirection.Left when items > 0:
                    return NavOutcome.AdjustDown;
                case NavDirection.Right when items > 0:
                    return NavOutcome.AdjustUp;
            }
            return NavOutcome.Nothing;
        }
    }

    /// <summary>
    /// Which part of a long list is shown (playtest round, 2026-11-11). The window keeps still while
    /// the chosen item is inside it and moves only as far as needed to bring it in, so a step moves
    /// the highlight, not the whole list. Before, the window was worked out afresh from the top (the
    /// reviews) or centred on the choice (the recipes) on every step, so the list jumped under the
    /// player's eye.
    /// </summary>
    public static class ScrollWindow
    {
        /// <summary>
        /// The first item to show, given the one shown first before, the chosen item, each item's
        /// height (gaps included) and the height there is.
        /// </summary>
        public static int First(int previousFirst, int selected, IReadOnlyList<float> heights, float space)
        {
            var count = heights.Count;
            if (count == 0)
                return 0;
            selected = Math.Clamp(selected, 0, count - 1);
            var first = Math.Clamp(previousFirst, 0, count - 1);
            if (selected < first)
                first = selected;
            // Drop items off the top until everything from the first down to the chosen one fits.
            while (first < selected && Span(heights, first, selected) > space)
                first++;
            // Don't leave a gap at the bottom when the list ends: pull earlier items back in.
            while (first > 0 && Span(heights, first - 1, count - 1) <= space)
                first--;
            return first;
        }

        /// <summary>The same for items of one height: <paramref name="fits"/> of them show at once.</summary>
        public static int First(int previousFirst, int selected, int count, int fits)
        {
            if (count <= 0 || fits <= 0)
                return 0;
            selected = Math.Clamp(selected, 0, count - 1);
            var first = Math.Clamp(previousFirst, 0, Math.Max(0, count - fits));
            if (selected < first)
                first = selected;
            else if (selected >= first + fits)
                first = selected - fits + 1;
            return first;
        }

        private static float Span(IReadOnlyList<float> heights, int from, int to)
        {
            var total = 0f;
            for (var i = from; i <= to; i++)
                total += heights[i];
            return total;
        }
    }
}
