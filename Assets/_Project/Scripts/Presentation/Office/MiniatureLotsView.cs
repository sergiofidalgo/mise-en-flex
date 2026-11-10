using Game.Gameplay.Office;
using UnityEngine;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The lots on the city miniature (Phase 6, milestone 2): each shows a red flag once owned or a
    /// gold "for sale" board while on sale, and the highlighted one stands in a pulsing ring under a
    /// bobbing gold diamond, so everyone round the table sees which lot the card is about. Each pin's
    /// children are named Owned, ForSale and Highlight (with Ring and Marker) by the office builder.
    /// </summary>
    public sealed class MiniatureLotsView : MonoBehaviour
    {
        [SerializeField] private MiniatureTable _table;
        [Tooltip("How high the highlighted lot's marker bobs (m, at the miniature's scale) and how fast (Hz).")]
        [SerializeField, Min(0f)] private float _bob = 0.04f;
        [SerializeField, Min(0f)] private float _bobRate = 1.6f;
        [Tooltip("How much the highlight ring pulses in size, from its own.")]
        [SerializeField, Range(0f, 0.5f)] private float _pulse = 0.12f;

        private Transform[] _owned = new Transform[0];
        private Transform[] _forSale = new Transform[0];
        private Transform[] _highlights = new Transform[0];
        private Transform[] _rings = new Transform[0];
        private Transform[] _markers = new Transform[0];
        private Vector3[] _markerRest = new Vector3[0];
        private Vector3[] _ownedRest = new Vector3[0];
        private Vector3[] _forSaleRest = new Vector3[0];
        private Vector3[] _ringScale = new Vector3[0];

        private void Start()
        {
            if (_table == null)
                return;
            var pins = _table.Pins;
            _owned = new Transform[pins.Count];
            _forSale = new Transform[pins.Count];
            _highlights = new Transform[pins.Count];
            _rings = new Transform[pins.Count];
            _markers = new Transform[pins.Count];
            _markerRest = new Vector3[pins.Count];
            _ownedRest = new Vector3[pins.Count];
            _forSaleRest = new Vector3[pins.Count];
            _ringScale = new Vector3[pins.Count];
            for (var i = 0; i < pins.Count; i++)
            {
                if (pins[i] == null)
                    continue;
                _owned[i] = pins[i].Find("Owned");
                _forSale[i] = pins[i].Find("ForSale");
                _highlights[i] = pins[i].Find("Highlight");
                _rings[i] = pins[i].Find("Highlight/Ring");
                _markers[i] = pins[i].Find("Highlight/Marker");
                if (_markers[i] != null) _markerRest[i] = _markers[i].localPosition;
                if (_owned[i] != null) _ownedRest[i] = _owned[i].localPosition;
                if (_forSale[i] != null) _forSaleRest[i] = _forSale[i].localPosition;
                if (_rings[i] != null) _ringScale[i] = _rings[i].localScale;
            }
        }

        private void Update()
        {
            if (_table == null || _table.Campaign == null)
                return;
            var looking = _table.Current != MiniatureTable.Step.Closed;
            var wave = Mathf.Sin(Time.time * _bobRate * Mathf.PI * 2f);
            for (var i = 0; i < _owned.Length; i++)
            {
                var owned = _table.Campaign.Owned(i) != null;
                var selected = looking && i == _table.Selected;
                var lift = selected ? Vector3.up * (_bob * (0.5f + 0.5f * wave)) : Vector3.zero;
                if (_owned[i] != null)
                {
                    _owned[i].gameObject.SetActive(owned);
                    _owned[i].localPosition = _ownedRest[i] + lift;
                }
                if (_forSale[i] != null)
                {
                    _forSale[i].gameObject.SetActive(!owned);
                    _forSale[i].localPosition = _forSaleRest[i] + lift;
                }
                if (_highlights[i] != null)
                    _highlights[i].gameObject.SetActive(selected);
                if (_markers[i] != null)
                {
                    _markers[i].localPosition = _markerRest[i] + Vector3.up * (_bob * 1.5f * wave);
                    _markers[i].Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
                }
                if (_rings[i] != null)
                {
                    var size = 1f + _pulse * (0.5f + 0.5f * wave);
                    _rings[i].localScale = new Vector3(_ringScale[i].x * size, _ringScale[i].y, _ringScale[i].z * size);
                }
            }
        }
    }
}
