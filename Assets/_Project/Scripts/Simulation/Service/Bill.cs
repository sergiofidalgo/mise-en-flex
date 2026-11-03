using System;
using System.Linq;
using Game.Simulation.Quality;

namespace Game.Simulation.Service
{
    /// <summary>Tuning for what customers pay. Filled from the ServiceConfig asset.</summary>
    public sealed class PaymentSettings
    {
        /// <summary>Share of the menu price a customer pays for a dish scoring 0 (a score of 1 pays it all).</summary>
        public float MinPayFraction { get; set; } = 0.5f;

        /// <summary>Tip for a perfect dish served at once, as a fraction of what they paid.</summary>
        public float MaxTipFraction { get; set; } = 0.25f;

        /// <summary>Dish score below which there's no tip at all.</summary>
        public float TipFromScore { get; set; } = 0.6f;

        /// <summary>Share of the tip still left when served with no patience remaining.</summary>
        public float SlowTipFraction { get; set; } = 0.4f;
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
        /// something but a good one earns all of it. The tip scales up from a decent dish to a
        /// perfect one, then down with how long they waited: quality earns it, speed protects it.
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

            var tipQuality = settings.TipFromScore < 1f
                ? Math.Clamp((score.Overall - settings.TipFromScore) / (1f - settings.TipFromScore), 0f, 1f)
                : 0f;
            var speed = Lerp(settings.SlowTipFraction, 1f, patienceWhenServed);
            var tip = payment * settings.MaxTipFraction * tipQuality * speed;

            return new Bill(Round(payment), Round(tip));
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

        private static float Round(float money) => (float)Math.Round(money, MidpointRounding.AwayFromZero);
    }
}
