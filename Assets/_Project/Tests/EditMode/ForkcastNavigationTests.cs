using Game.Simulation.Days;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// forkcast's D-pad navigation (the playtest round, 2026-11-11): each player's own repeat, explicit
    /// neighbours between the chips and the list, and lists that scroll only as far as needed.
    /// </summary>
    public class ForkcastNavigationTests
    {
        // ---- NavRepeat ----

        private static NavRepeat Repeat() => new NavRepeat { Threshold = 0.5f, FirstDelay = 0.3f, Interval = 0.2f };

        [Test]
        public void A_press_steps_once_then_repeats_after_the_delay()
        {
            var nav = Repeat();
            Assert.That(nav.Update(0, 0f, -1f, 0f), Is.EqualTo(NavDirection.Down));
            Assert.That(nav.Update(0, 0f, -1f, 0.1f), Is.EqualTo(NavDirection.None));
            Assert.That(nav.Update(0, 0f, -1f, 0.29f), Is.EqualTo(NavDirection.None));
            Assert.That(nav.Update(0, 0f, -1f, 0.3f), Is.EqualTo(NavDirection.Down));
            Assert.That(nav.Update(0, 0f, -1f, 0.4f), Is.EqualTo(NavDirection.None));
            Assert.That(nav.Update(0, 0f, -1f, 0.5f), Is.EqualTo(NavDirection.Down));
        }

        [Test]
        public void An_idle_second_player_does_not_make_the_first_race()
        {
            // The old bug: one shared timer, reset by the idle player every frame.
            var nav = Repeat();
            var steps = 0;
            for (var frame = 0; frame < 15; frame++) // a quarter of a second at 60 fps
            {
                var now = frame / 60f;
                if (nav.Update(0, 0f, -1f, now) != NavDirection.None)
                    steps++;
                nav.Update(1, 0f, 0f, now);
            }
            Assert.That(steps, Is.EqualTo(1));
        }

        [Test]
        public void Two_players_each_step_on_their_own_press()
        {
            var nav = Repeat();
            Assert.That(nav.Update(0, 0f, 1f, 0f), Is.EqualTo(NavDirection.Up));
            Assert.That(nav.Update(1, 1f, 0f, 0.05f), Is.EqualTo(NavDirection.Right));
            Assert.That(nav.Update(0, 0f, 1f, 0.05f), Is.EqualTo(NavDirection.None));
        }

        [Test]
        public void Letting_go_and_pressing_again_steps_at_once()
        {
            var nav = Repeat();
            nav.Update(0, 0f, -1f, 0f);
            nav.Update(0, 0f, 0f, 0.05f);
            Assert.That(nav.Update(0, 0f, -1f, 0.1f), Is.EqualTo(NavDirection.Down));
        }

        [Test]
        public void A_change_of_direction_steps_at_once()
        {
            var nav = Repeat();
            nav.Update(0, 0f, -1f, 0f);
            Assert.That(nav.Update(0, -1f, 0f, 0.05f), Is.EqualTo(NavDirection.Left));
        }

        [Test]
        public void A_diagonal_goes_by_its_larger_axis_and_a_small_push_does_nothing()
        {
            var nav = Repeat();
            Assert.That(nav.DirectionOf(0.7f, 0.71f), Is.EqualTo(NavDirection.Up));
            Assert.That(nav.DirectionOf(-0.8f, 0.6f), Is.EqualTo(NavDirection.Left));
            Assert.That(nav.DirectionOf(0.2f, 0.3f), Is.EqualTo(NavDirection.None));
        }

        // ---- ChipsOverList ----

        [Test]
        public void Down_and_up_walk_the_list_without_jumping()
        {
            var focus = new ChipsOverList();
            Assert.That(focus.Move(NavDirection.Down, 5, 3), Is.EqualTo(NavOutcome.Moved));
            Assert.That(focus.Item, Is.EqualTo(1));
            focus.Move(NavDirection.Down, 5, 3);
            Assert.That(focus.Move(NavDirection.Down, 5, 3), Is.EqualTo(NavOutcome.Nothing), "the bottom stays put");
            Assert.That(focus.Item, Is.EqualTo(2));
            focus.Move(NavDirection.Up, 5, 3);
            Assert.That(focus.Item, Is.EqualTo(1));
        }

        [Test]
        public void Up_from_the_top_rises_to_the_chosen_chip_and_down_comes_back_to_the_top()
        {
            var focus = new ChipsOverList();
            focus.Reset(chip: 2);
            Assert.That(focus.Move(NavDirection.Up, 5, 3), Is.EqualTo(NavOutcome.Moved));
            Assert.That(focus.OnChips, Is.True);
            Assert.That(focus.Chip, Is.EqualTo(2), "the chip in force, not the first");
            Assert.That(focus.Move(NavDirection.Up, 5, 3), Is.EqualTo(NavOutcome.Nothing), "nothing above the chips");
            Assert.That(focus.Move(NavDirection.Down, 5, 3), Is.EqualTo(NavOutcome.Moved));
            Assert.That(focus.OnChips, Is.False);
            Assert.That(focus.Item, Is.EqualTo(0));
        }

        [Test]
        public void Left_and_right_step_along_the_chips_without_wrapping()
        {
            var focus = new ChipsOverList();
            focus.Reset(onChips: true);
            Assert.That(focus.Move(NavDirection.Left, 3, 4), Is.EqualTo(NavOutcome.Nothing));
            Assert.That(focus.Move(NavDirection.Right, 3, 4), Is.EqualTo(NavOutcome.ChipChanged));
            focus.Move(NavDirection.Right, 3, 4);
            Assert.That(focus.Move(NavDirection.Right, 3, 4), Is.EqualTo(NavOutcome.Nothing));
            Assert.That(focus.Chip, Is.EqualTo(2));
        }

        [Test]
        public void Left_and_right_in_the_list_adjust_the_item_and_keep_the_focus()
        {
            var focus = new ChipsOverList();
            focus.Move(NavDirection.Down, 2, 4);
            Assert.That(focus.Move(NavDirection.Right, 2, 4), Is.EqualTo(NavOutcome.AdjustUp));
            Assert.That(focus.Move(NavDirection.Left, 2, 4), Is.EqualTo(NavOutcome.AdjustDown));
            Assert.That(focus.Item, Is.EqualTo(1));
            Assert.That(focus.OnChips, Is.False);
        }

        [Test]
        public void An_empty_list_keeps_the_focus_on_the_chips()
        {
            var focus = new ChipsOverList();
            focus.Reset(onChips: true);
            Assert.That(focus.Move(NavDirection.Down, 5, 0), Is.EqualTo(NavOutcome.Nothing));
            Assert.That(focus.OnChips, Is.True);
        }

        [Test]
        public void A_tab_without_chips_never_rises_above_its_list()
        {
            var focus = new ChipsOverList();
            Assert.That(focus.Move(NavDirection.Up, 0, 3), Is.EqualTo(NavOutcome.Nothing));
            Assert.That(focus.OnChips, Is.False);
        }

        [Test]
        public void A_list_that_shrinks_keeps_the_focus_inside_it()
        {
            var focus = new ChipsOverList();
            focus.Set(false, 0, 4);
            focus.Clamp(0, 2);
            Assert.That(focus.Item, Is.EqualTo(1));
        }

        // ---- ScrollWindow ----

        [Test]
        public void The_window_keeps_still_while_the_choice_is_inside_it()
        {
            // Five items of 100 in 300: three fit.
            var heights = new float[] { 100, 100, 100, 100, 100 };
            Assert.That(ScrollWindow.First(0, 2, heights, 300f), Is.EqualTo(0));
            Assert.That(ScrollWindow.First(0, 3, heights, 300f), Is.EqualTo(1), "scrolls one to bring the fourth in");
            Assert.That(ScrollWindow.First(1, 2, heights, 300f), Is.EqualTo(1), "back up inside the window: no jump");
            Assert.That(ScrollWindow.First(1, 1, heights, 300f), Is.EqualTo(1));
            Assert.That(ScrollWindow.First(1, 0, heights, 300f), Is.EqualTo(0), "and only scrolls back past its top");
        }

        [Test]
        public void The_window_never_leaves_a_gap_below_the_list_end()
        {
            var heights = new float[] { 100, 100, 100, 100 };
            Assert.That(ScrollWindow.First(3, 3, heights, 300f), Is.EqualTo(1));
        }

        [Test]
        public void A_tall_item_takes_the_room_it_needs()
        {
            var heights = new float[] { 100, 250, 100 };
            Assert.That(ScrollWindow.First(0, 1, heights, 300f), Is.EqualTo(1));
        }

        [Test]
        public void Even_items_follow_the_same_rule()
        {
            Assert.That(ScrollWindow.First(0, 5, 10, 6), Is.EqualTo(0));
            Assert.That(ScrollWindow.First(0, 6, 10, 6), Is.EqualTo(1));
            Assert.That(ScrollWindow.First(1, 3, 10, 6), Is.EqualTo(1), "no recentring");
            Assert.That(ScrollWindow.First(4, 2, 10, 6), Is.EqualTo(2));
            Assert.That(ScrollWindow.First(9, 9, 3, 6), Is.EqualTo(0), "a short list shows from its top");
        }
    }
}
