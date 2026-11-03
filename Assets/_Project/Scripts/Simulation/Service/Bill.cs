using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Quality;

namespace Game.Simulation.Service
{
    /// <summary>Tuning for what customers pay. Filled from the ServiceConfig asset.</summary>
    public sealed class PaymentSettings
    {
        /// <summary>Share of the menu price a customer pays for a dish scoring 0 (a score of 1 pays it all).</summary>
        public float MinPayFraction { get; set; } = 0.5f;

        /// <summary>
        /// Dish score → tip as a fraction of the menu price, ascending by score, blended linearly
        /// between stops. No tip below the first stop, the full last stop above the last. Perfection
        /// (from 99.5%, since a real steak always loses a trace of freshness) is worth a whole extra
        /// price, and even a small flaw costs most of that (decisions 2026-11-03).
        /// </summary>
        public IReadOnlyList<TipStop> TipCurve { get; set; } = new[]
        {
            new TipStop(0.80f, 0f),
            new TipStop(0.90f, 0.10f),
            new TipStop(0.95f, 0.20f),
            new TipStop(0.99f, 0.50f),
            new TipStop(0.995f, 1.00f),
        };

        /// <summary>Patience left at or above which the wait doesn't touch the tip.</summary>
        public float SlowTipFrom { get; set; } = 0.5f;

        /// <summary>Share of the tip still left when served with no patience remaining.</summary>
        public float SlowTipFraction { get; set; } = 0.4f;

        /// <summary>Money lost when a customer walks out, as a fraction of their order's menu price.</summary>
        public float WalkOutPenaltyFraction { get; set; } = 0.5f;
    }

    /// <summary>A point on the tip curve: at this dish score, tip this fraction of the menu price.</summary>
    public readonly struct TipStop
    {
        public TipStop(float score, float tipFraction)
        {
            Score = score;
            TipFraction = tipFraction;
        }

        public float Score { get; }
        public float TipFraction { get; }
    }

    /// <summary>What one customer paid, in whole money units.</summary>
    public readonly struct Bill
    {
        public Bill(float payment, float tip)
        {
            Payment = payment;
            Tip = tip;
        }

        public float Payment { get; }
        public float Tip { get; }
        public float Total => Payment + Tip;
    }

    /// <summary>Turns a served dish into money (decision 2026-11-03).</summary>
    public static class Billing
    {
        /// <summary>
        /// Customers pay for what reached the plate, reduced by quality, so a bad dish still earns
        /// something but a good one earns all of it. The tip follows the steep tip curve, based on
        /// the menu price, then shrinks only if they waited long: quality earns it, speed protects it.
        /// </summary>
        public static Bill Calculate(Order order, DishScore score, float patienceWhenServed, PaymentSettings settings)
        {
            if (order == null || score == null)
                return new Bill(0f, 0f);

            var price = 0f;
            if (!score.Missing.Contains(order.MainTypeId))
                price += order.MainPrice;
            if (order.SideTypeId != null && !score.Missing.Contains(order.SideTypeId))
                price += order.SidePrice;

            var payment = price * Lerp(settings.MinPayFraction, 1f, score.Overall);

            // Full tip while they still have patience to spare (everyone waits a little for a
            // cooked steak), falling to SlowTipFraction as patience runs out.
            var speed = settings.SlowTipFrom > 0f
                ? Lerp(settings.SlowTipFraction, 1f, patienceWhenServed / settings.SlowTipFrom)
                : 1f;
            var tip = price * TipFraction(score.Overall, settings.TipCurve) * speed;

            return new Bill(Round(payment), Round(tip));
        }

        /// <summary>What a walk-out costs: a share of what their order would have paid.</summary>
        public static float WalkOutPenalty(Order order, PaymentSettings settings)
        {
            if (order == null)
                return 0f;
            var price = order.MainPrice + (order.SideTypeId != null ? order.SidePrice : 0f);
            return Round(price * settings.WalkOutPenaltyFraction);
        }

        private static float TipFraction(float score, IReadOnlyList<TipStop> curve)
        {
            if (curve.Count == 0 || score < curve[0].Score)
                return 0f;
            for (var i = 1; i < curve.Count; i++)
            {
                if (score <= curve[i].Score)
                {
                    var from = curve[i - 1];
                    var to = curve[i];
                    var t = to.Score > from.Score ? (score - from.Score) / (to.Score - from.Score) : 1f;
                    return Lerp(from.TipFraction, to.TipFraction, t);
                }
            }
            return curve[curve.Count - 1].TipFraction;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

        private static float Round(float money) => (float)Math.Round(money, MidpointRounding.AwayFromZero);
    }
}
