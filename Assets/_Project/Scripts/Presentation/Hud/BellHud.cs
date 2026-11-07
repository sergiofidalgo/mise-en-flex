using System.Collections.Generic;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation.Hud
{
    /// <summary>A label over each counter where the bell has been rung for a plate. The bell's sound is in <see cref="KitchenAudio"/>.</summary>
    public sealed class BellHud : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private string _text = "Order up!";
        [Tooltip("Metres above the floor where the label sits.")]
        [SerializeField, Min(0f)] private float _height = 1.2f;
        [SerializeField] private Vector2 _padding = new Vector2(10f, 4f);

        private readonly Dictionary<CounterStation, WorldLabel> _labels = new Dictionary<CounterStation, WorldLabel>();

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null || _root.Services == null)
                return;

            var bell = _root.Services.Bell;
            foreach (var counter in _root.Services.Stations.All.OfType<CounterStation>())
            {
                var rung = bell.IsRung(counter.Slot);
                if (!_labels.TryGetValue(counter, out var label))
                {
                    if (!rung)
                        continue;
                    label = WorldLabel.Create(_style, _canvas, "BellLabel", _padding, new Vector2(0.5f, 0f));
                    _labels[counter] = label;
                }

                if (rung)
                    label.Show(_text, counter.transform.position + Vector3.up * _height, camera, _canvas);
                else
                    label.Hide();
            }
        }
    }
}
