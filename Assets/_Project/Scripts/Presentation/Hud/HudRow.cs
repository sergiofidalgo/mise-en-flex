using System.Collections.Generic;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// Spreads cards that want to sit side by side in one row so none overlap, each staying as near
    /// to where it wanted to be as the others allow: cards that would overlap move apart as a group,
    /// centred on where they wanted to be on average, and the row is kept within the screen. With four
    /// players, cards over chefs standing close together (the tailor's ladders) would otherwise stack.
    /// </summary>
    public static class HudRow
    {
        private struct Group
        {
            public int First;    // index into the sorted order
            public int Count;
            public float Width;  // the cards and the gaps between them
            public float Wanted; // sum over its cards of (wanted left - offset in the group)
            public float Left;
        }

        /// <param name="lefts">Each card's wanted left edge; replaced by where it goes.</param>
        /// <param name="widths">Each card's width.</param>
        /// <param name="gap">Room kept between neighbours.</param>
        /// <param name="min">The leftmost a card may go.</param>
        /// <param name="max">The rightmost a card's right edge may go.</param>
        public static void Spread(IList<float> lefts, IReadOnlyList<float> widths, float gap, float min, float max)
        {
            var count = lefts.Count;
            if (count < 2)
                return;

            var order = new List<int>(count);
            for (var i = 0; i < count; i++)
                order.Add(i);
            order.Sort((a, b) => (lefts[a] + widths[a] / 2f).CompareTo(lefts[b] + widths[b] / 2f));

            var groups = new List<Group>(count);
            for (var k = 0; k < count; k++)
            {
                var i = order[k];
                groups.Add(Clamp(new Group { First = k, Count = 1, Width = widths[i], Wanted = lefts[i] }, min, max));

                // Merge into the group before while they overlap; the merged group may then reach
                // back into the one before that.
                while (groups.Count > 1)
                {
                    var last = groups[groups.Count - 1];
                    var before = groups[groups.Count - 2];
                    if (before.Left + before.Width + gap <= last.Left)
                        break;
                    var offset = before.Width + gap;
                    var merged = new Group
                    {
                        First = before.First,
                        Count = before.Count + last.Count,
                        Width = before.Width + gap + last.Width,
                        // Each card in the later group now sits `offset` further into the group.
                        Wanted = before.Wanted + last.Wanted - offset * last.Count,
                    };
                    groups.RemoveRange(groups.Count - 2, 2);
                    groups.Add(Clamp(merged, min, max));
                }
            }

            foreach (var group in groups)
            {
                var x = group.Left;
                for (var k = group.First; k < group.First + group.Count; k++)
                {
                    var i = order[k];
                    lefts[i] = x;
                    x += widths[i] + gap;
                }
            }
        }

        // Where a group goes: the average of where its cards wanted it, kept on screen. A row wider
        // than the screen keeps its left edge on screen.
        private static Group Clamp(Group group, float min, float max)
        {
            var left = group.Wanted / group.Count;
            if (left + group.Width > max)
                left = max - group.Width;
            if (left < min)
                left = min;
            group.Left = left;
            return group;
        }
    }
}
