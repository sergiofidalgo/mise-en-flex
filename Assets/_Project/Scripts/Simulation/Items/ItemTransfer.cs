namespace Game.Simulation.Items
{
    public enum InteractionOutcome
    {
        /// <summary>Nothing could happen (e.g. full counter, empty hands at an empty counter).</summary>
        Nothing,
        /// <summary>Something went from the station into the player's hands (or into what they hold).</summary>
        PickedUp,
        /// <summary>Something went from the player's hands onto or into the station.</summary>
        Placed,
        /// <summary>Contents moved between two containers (plating, pouring, scraping into the bin).</summary>
        Combined,
    }

    /// <summary>
    /// The contextual Interact rule. In order:
    /// <list type="number">
    /// <item>Empty hands take what's on the station; a pan or plate comes with its food. A container with food stays and gives one portion.</item>
    /// <item>Full hands put down if the station accepts what they hold.</item>
    /// <item>Food goes into a pan or onto a plate standing on the station.</item>
    /// <item>A held pan or plate scoops up food standing on the station.</item>
    /// <item>Two containers: contents move between them (plating from a pan, back into a pan).</item>
    /// <item>A held container's contents go into the station if it takes them (scraping into the bin).</item>
    /// <item>A held tray picks up a plate from a plate source (the stack, the sink, the pass).</item>
    /// <item>Food held at the clean plate stack (or the pile of delivery boxes) goes onto a fresh one, and the chef holds it (decision 2026-11-08).</item>
    /// </list>
    /// </summary>
    public static class ItemTransfer
    {
        public static InteractionOutcome Interact(ItemSlot hands, IItemContainer target)
        {
            var onStation = (target as IItemHolder)?.Current;

            if (hands.IsEmpty)
            {
                if (onStation is FoodContainer portions && !portions.IsEmpty)
                {
                    hands.Put(portions.Take());
                    return InteractionOutcome.PickedUp;
                }

                if (!target.CanTake)
                    return InteractionOutcome.Nothing;

                hands.Put(target.Take());
                return InteractionOutcome.PickedUp;
            }

            var held = hands.Current;
            if (target.CanAccept(held))
            {
                target.Put(hands.Take());
                return InteractionOutcome.Placed;
            }

            if (onStation != null)
            {
                if (onStation is IItemCollection standing && standing.CanAccept(held))
                {
                    standing.Put(hands.Take());
                    return InteractionOutcome.Placed;
                }

                if (held is IItemCollection holding)
                {
                    // A plate or pan takes one portion from a container; a tray moves as many as fit.
                    if (onStation is FoodContainer container && !(held is Tray) && !container.IsEmpty)
                    {
                        if (!holding.CanAccept(container.Items[0]))
                            return InteractionOutcome.Nothing;
                        holding.Put(container.Take());
                        return InteractionOutcome.PickedUp;
                    }

                    if (holding.CanAccept(onStation))
                    {
                        holding.Put(target.Take());
                        return InteractionOutcome.PickedUp;
                    }

                    if (onStation is IItemCollection other && (Pour(holding, other) > 0 || Pour(other, holding) > 0))
                        return InteractionOutcome.Combined;
                }
            }

            if (held is IItemCollection source && Pour(source, target) > 0)
                return InteractionOutcome.Combined;

            if (held is Tray tray && tray.HasRoom && target is IPlateSource && target.CanTake)
            {
                tray.Put(target.Take());
                return InteractionOutcome.PickedUp;
            }

            if (target is IFreshPlateSource stack && CanPlate(stack, held))
            {
                var plate = (Plate)stack.Take();
                plate.Put(hands.Take());
                hands.Put(plate);
                return InteractionOutcome.PickedUp;
            }

            return InteractionOutcome.Nothing;
        }

        /// <summary>
        /// True if food held at the clean plate stack would go onto a fresh plate from it: there's a
        /// plate, and it takes the food (not a pan, plate or container, which hold their own).
        /// </summary>
        public static bool CanPlate(IFreshPlateSource stack, Item held)
        {
            if (stack == null || !stack.CanTake || held == null || held is IItemCollection)
                return false;
            return stack.PeekAccepts(held);
        }

        // Moves items from the end of `from` into `to` for as long as `to` accepts them. A tray holds
        // a mix, so anything on it that `to` takes goes, wherever it is on the tray.
        private static int Pour(IItemCollection from, IItemContainer to)
        {
            var moved = 0;
            if (from is Tray tray)
            {
                for (var i = tray.Items.Count - 1; i >= 0; i--)
                {
                    if (!to.CanAccept(tray.Items[i]))
                        continue;
                    to.Put(tray.TakeAt(i));
                    moved++;
                }
                return moved;
            }


            while (from.Items.Count > 0 && to.CanAccept(from.Items[from.Items.Count - 1]))
            {
                to.Put(from.Take());
                moved++;
            }
            return moved;
        }
    }
}
