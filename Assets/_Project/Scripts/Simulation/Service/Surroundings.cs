using System;
using System.Collections.Generic;

namespace Game.Simulation.Service
{
    /// <summary>Tuning for what the room around a guest does to their night (decisions 2026-11-11). Filled from TuningConfig.</summary>
    public sealed class SurroundingsSettings
    {
        /// <summary>Metres from the customer door within which the wheelie bin is smelt (user's call: 4).</summary>
        public float BinSmellRadius { get; set; } = 4f;

        /// <summary>Score (0..1) lost with the bin right at the door (user's call: up to 1 star, 0.2).</summary>
        public float BinSmellMaxPenalty { get; set; } = 0.2f;

        /// <summary>From this penalty up, the smell is what the review may be about (0.1: half a star, the bin within about 2 m).</summary>
        public float BinSmellNoticedFrom { get; set; } = 0.1f;

        /// <summary>Tonight's average stars from which an open kitchen is a show rather than a nuisance (user's call: 4.5).</summary>
        public float GreatNightStars { get; set; } = 4.5f;

        /// <summary>Score gained or lost per appliance in the dining room or table in the kitchen (0.1: half a star).</summary>
        public float OpenKitchenPerPiece { get; set; } = 0.1f;

        /// <summary>The most an open kitchen can add or take (user's call: up to 1 star, 0.2).</summary>
        public float OpenKitchenMax { get; set; } = 0.2f;
    }

    /// <summary>How guests take an open kitchen tonight.</summary>
    public enum OpenKitchenReaction
    {
        /// <summary>Kitchen and dining room are kept apart.</summary>
        None,

        /// <summary>A great night: the chef's-table show they love.</summary>
        Show,

        /// <summary>An average or poor night: mess and noise at the table.</summary>
        Complaint,
    }

    /// <summary>What a guest's surroundings did to their dish's score.</summary>
    public readonly struct SurroundingsEffect
    {
        public SurroundingsEffect(float binSmell, bool binNoticed, float openKitchen, OpenKitchenReaction reaction)
        {
            BinSmell = binSmell;
            BinNoticed = binNoticed;
            OpenKitchen = openKitchen;
            Reaction = reaction;
        }

        /// <summary>0..1 taken off for the bin's smell by the door.</summary>
        public float BinSmell { get; }

        /// <summary>True if the smell was strong enough to be what the guest remembers, after the food and dirt.</summary>
        public bool BinNoticed { get; }

        /// <summary>Signed: added for a chef's-table show, negative for mess and noise.</summary>
        public float OpenKitchen { get; }

        public OpenKitchenReaction Reaction { get; }

        /// <summary>The net change to the score.</summary>
        public float Total => OpenKitchen - BinSmell;
    }

    /// <summary>What kind of piece stands outside its usual room.</summary>
    public enum OutOfPlaceKind
    {
        ApplianceInDiningRoom,
        TableInKitchen,
    }

    /// <summary>A piece outside its usual room, and the floor beside it where its extra mess lands.</summary>
    public readonly struct OutOfPlacePiece
    {
        public OutOfPlacePiece(OutOfPlaceKind kind, float x, float z)
        {
            Kind = kind;
            X = x;
            Z = z;
        }

        public OutOfPlaceKind Kind { get; }
        public float X { get; }
        public float Z { get; }
    }

    /// <summary>
    /// The room around the guests (decisions 2026-11-11): a wheelie bin near the customer door smells,
    /// softly and more the closer it stands; and kitchens and dining rooms that mix are a show on a
    /// great night and a nuisance on any other. Both are learnable in play, never a disaster.
    /// </summary>
    public static class Surroundings
    {
        /// <summary>
        /// What the bin's smell takes off: the full penalty with the bin at the door, fading in a
        /// straight line to nothing at the radius. Linear so a player who moves the bin a metre
        /// further sees a steady, predictable gain.
        /// </summary>
        /// <param name="distance">Metres from the door to the bin's nearest side; infinity for no bin.</param>
        public static float BinSmell(float distance, SurroundingsSettings settings)
        {
            if (float.IsNaN(distance) || settings.BinSmellRadius <= 0f || distance >= settings.BinSmellRadius)
                return 0f;
            var closeness = 1f - Math.Max(0f, distance) / settings.BinSmellRadius;
            return Math.Max(0f, settings.BinSmellMaxPenalty) * closeness;
        }

        /// <summary>
        /// A great night is one where tonight's food has been praise-worthy so far and this guest
        /// wasn't kept waiting (user's call, 2026-11-11). Anything less is average or poor.
        /// </summary>
        /// <param name="nightStars">Tonight's average stars, this guest's own dish included.</param>
        /// <param name="patienceWhenServed">This guest's patience left when the food arrived, 0..1.</param>
        /// <param name="lowPatience">Below this, the wait counts as long (the slow-service mark).</param>
        public static bool IsGreatNight(float nightStars, float patienceWhenServed, float lowPatience, SurroundingsSettings settings) =>
            nightStars >= settings.GreatNightStars && patienceWhenServed >= lowPatience;

        /// <summary>How much an open kitchen moves a guest's score, either way: per piece, capped.</summary>
        public static float OpenKitchenSize(int appliancesInDiningRoom, int tablesInKitchen, SurroundingsSettings settings)
        {
            var pieces = Math.Max(0, appliancesInDiningRoom) + Math.Max(0, tablesInKitchen);
            return Math.Min(pieces * Math.Max(0f, settings.OpenKitchenPerPiece), Math.Max(0f, settings.OpenKitchenMax));
        }

        /// <summary>Everything the room does to one guest's score.</summary>
        public static SurroundingsEffect For(
            float binDistance, int appliancesInDiningRoom, int tablesInKitchen,
            float nightStars, float patienceWhenServed, float lowPatience, SurroundingsSettings settings)
        {
            var smell = BinSmell(binDistance, settings);
            var size = OpenKitchenSize(appliancesInDiningRoom, tablesInKitchen, settings);
            var reaction = size <= 0f ? OpenKitchenReaction.None
                : IsGreatNight(nightStars, patienceWhenServed, lowPatience, settings) ? OpenKitchenReaction.Show
                : OpenKitchenReaction.Complaint;
            var openKitchen = reaction == OpenKitchenReaction.Show ? size : reaction == OpenKitchenReaction.Complaint ? -size : 0f;
            return new SurroundingsEffect(smell, smell > 0f && smell >= settings.BinSmellNoticedFrom, openKitchen, reaction);
        }

        /// <summary>
        /// Tonight's average stars with this guest's own dish counted in. Everyone's stars are taken
        /// before their surroundings, so a show doesn't feed itself and a complaint doesn't spiral.
        /// </summary>
        /// <param name="tonight">Tonight's customers; those not served yet are skipped.</param>
        /// <param name="ownStars">This guest's stars for their dish, before surroundings.</param>
        public static float NightStars(IEnumerable<Customer> tonight, float ownStars)
        {
            var total = ownStars;
            var count = 1;
            foreach (var customer in tonight)
            {
                var stars = customer.StarsBeforeSurroundings;
                if (!stars.HasValue)
                    continue;
                total += stars.Value;
                count++;
            }
            return total / count;
        }

        /// <summary>How many of each kind stand out of place.</summary>
        public static (int AppliancesInDiningRoom, int TablesInKitchen) Count(IEnumerable<OutOfPlacePiece> pieces)
        {
            var appliances = 0;
            var tables = 0;
            if (pieces != null)
            {
                foreach (var piece in pieces)
                {
                    if (piece.Kind == OutOfPlaceKind.ApplianceInDiningRoom)
                        appliances++;
                    else
                        tables++;
                }
            }
            return (appliances, tables);
        }
    }
}
