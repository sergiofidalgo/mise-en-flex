using Game.Gameplay.Items;
using Game.Gameplay.Stations;
using Game.Simulation.CloseUp;
using UnityEngine;

namespace Game.Gameplay.CloseUp
{
    /// <summary>
    /// The hob's close-up: an overhead camera on the pan, with the heat on the triggers and A to
    /// flip the selected food (picked with the stick). Logic lives in <see cref="HobTechnique"/>.
    /// </summary>
    public sealed class HobCloseUpTechnique : MonoBehaviour, ICloseUpTechnique
    {
        [SerializeField] private Camera _camera;

        [Tooltip("Floats above the selected food so the player knows what A will flip.")]
        [SerializeField] private Transform _selectionMarker;

        [Tooltip("Metres the marker floats above the food.")]
        [SerializeField, Min(0f)] private float _markerHeight = 0.08f;

        private HobStation _hob;
        private KitchenServices _services;
        private HobTechnique _logic;

        public TechniqueResult Result => new TechniqueResult(false, 0f);

        public void Setup(Interactable station, KitchenServices services)
        {
            _hob = station as HobStation;
            _services = services;
            _camera.enabled = false;
            _selectionMarker.gameObject.SetActive(false);
        }

        public void Begin(RenderTexture target)
        {
            _logic ??= new HobTechnique(_hob.Hob, _services.Tuning.HeatDialSpeed);
            _camera.targetTexture = target;
            _camera.enabled = true;
        }

        public void HandleInput(CloseUpInput input, float deltaTime)
        {
            _logic.DialSpeed = _services.Tuning.HeatDialSpeed;
            _logic.HandleInput(input, deltaTime);
        }

        public void Tick(float deltaTime)
        {
            var selected = _logic.Selected;
            ItemView view = null;
            if (selected != null)
                _services.ItemViews.TryGetView(selected, out view);

            _selectionMarker.gameObject.SetActive(view != null);
            if (view != null)
                _selectionMarker.position = view.transform.position + Vector3.up * _markerHeight;
        }

        public void Finish()
        {
            _camera.enabled = false;
            _camera.targetTexture = null;
            _selectionMarker.gameObject.SetActive(false);
        }
    }
}
