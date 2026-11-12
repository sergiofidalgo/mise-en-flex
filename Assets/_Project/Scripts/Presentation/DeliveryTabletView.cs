using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// The order tablet on a delivery counter (decision 2026-11-12): when an order comes in for one
    /// of this counter's places its screen flashes the app's lime and it buzzes on its stand, and
    /// the screen stays lit while any of the counter's orders is open. The ping itself is
    /// <see cref="KitchenAudio"/>'s; the order's bubble is the HUD's.
    /// </summary>
    public sealed class DeliveryTabletView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private DeliveryCounterStation _counter;
        [SerializeField] private Transform _tablet;
        [SerializeField] private Renderer _screen;
        [SerializeField] private Color _off = new Color(0.1f, 0.12f, 0.1f);
        [SerializeField] private Color _lit = new Color(0.55f, 0.86f, 0.22f);
        [Tooltip("Real seconds it buzzes when an order comes in.")]
        [SerializeField, Min(0f)] private float _buzzSeconds = 1.1f;
        [Tooltip("Degrees it rocks while buzzing, and how many times a second.")]
        [SerializeField, Min(0f)] private float _buzzAngle = 7f;
        [SerializeField, Min(0f)] private float _buzzRate = 22f;

        private KitchenServices _services;
        private ServiceSimulation _listening;
        private MaterialPropertyBlock _block;
        private float _buzzUntil = -1f;
        private Quaternion _rest;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_tablet != null)
                _rest = _tablet.localRotation;
        }

        private void OnDestroy() => Listen(null);

        private void Update()
        {
            // A counter bought mid-campaign has no services until the kitchen has started; found then.
            if (_services == null)
            {
                var root = FindFirstObjectByType<KitchenCompositionRoot>();
                _services = root != null ? root.Services : null;
                if (_services == null)
                    return;
            }
            if (_listening != _services.Service)
                Listen(_services.Service);

            var open = false;
            foreach (var order in _services.Service.Deliveries.Orders)
                open |= order.HoldsSlot && _counter.AnchorOfSlot(order.Slot) != null;

            var buzzing = Time.unscaledTime < _buzzUntil;
            if (_screen != null)
            {
                // Lit while an order's open; flashing with the buzz.
                var flash = buzzing ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _buzzRate) : 1f;
                _screen.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, open || buzzing ? Color.Lerp(_off, _lit, flash) : _off);
                _screen.SetPropertyBlock(_block);
            }
            if (_tablet != null)
            {
                var left = buzzing ? (_buzzUntil - Time.unscaledTime) / Mathf.Max(0.01f, _buzzSeconds) : 0f;
                _tablet.localRotation = _rest * Quaternion.Euler(0f, 0f, left * _buzzAngle * Mathf.Sin(Time.unscaledTime * _buzzRate * 2f * Mathf.PI));
            }
        }

        private void Listen(ServiceSimulation service)
        {
            if (_listening != null)
                _listening.Deliveries.Ordered -= HandleOrdered;
            _listening = service;
            if (_listening != null)
                _listening.Deliveries.Ordered += HandleOrdered;
        }

        private void HandleOrdered(DeliveryOrder order)
        {
            if (_counter != null && _counter.AnchorOfSlot(order.Slot) != null)
                _buzzUntil = Time.unscaledTime + _buzzSeconds;
        }
    }
}
