using System.Runtime.CompilerServices;
using Game.Gameplay.Items;
using Game.Simulation.Cooking;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Lets players read food by eye (decision 2026-11-02: doneness mostly from visual cues).
    /// The visible top surface follows its browning; for layered food like steak, the edge is
    /// three bands coloured by how hot each layer has been, so a cooked band creeps in from the
    /// seared sides and the middle band shows doneness, as when a cook looks at a steak's side.
    /// Old food shows its age on top of that (decision 2026-11-11, <see cref="FreshnessLook"/>):
    /// stale food dulls, and spoiled food goes mouldy, with fuzzy spots.
    /// </summary>
    public sealed class IngredientView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BandBottomId = Shader.PropertyToID("_BandBottom");
        private static readonly int BandCoreId = Shader.PropertyToID("_BandCore");
        private static readonly int BandTopId = Shader.PropertyToID("_BandTop");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int BrowningId = Shader.PropertyToID("_Browning");
        private static readonly int CrustBottomId = Shader.PropertyToID("_CrustBottom");
        private static readonly int BrowningBottomId = Shader.PropertyToID("_BrowningBottom");

        [SerializeField] private ItemView _itemView;

        [Tooltip("The surface seen from above; coloured by the up side's browning.")]
        [SerializeField] private Renderer _surface;

        [Tooltip("Optional edge bands: bottom, middle (core), top. Coloured by each layer's peak temperature.")]
        [SerializeField] private Renderer[] _bands = System.Array.Empty<Renderer>();

        [Tooltip("The surface is one mesh using the Game/Food/Layered shader, which draws the bands itself.")]
        [SerializeField] private bool _layeredShader;

        [Tooltip("Each piece of layered food gets a random size within this range, so no two look the same.")]
        [SerializeField] private Vector2 _sizeVariation = new Vector2(0.92f, 1.06f);

        [Tooltip("Browning as a fraction of 'burnt' (0..1) → colour. Starts from the raw colour.")]
        [SerializeField] private ColorStop[] _browning =
        {
            new ColorStop(0.25f, new Color(0.80f, 0.55f, 0.30f)), // golden
            new ColorStop(0.50f, new Color(0.50f, 0.28f, 0.13f)), // a good crust
            new ColorStop(0.75f, new Color(0.25f, 0.13f, 0.07f)), // dark
            new ColorStop(1.00f, new Color(0.07f, 0.06f, 0.06f)), // burnt
        };

        [Tooltip("Where the stale and spoiled lines are. Empty: 35% and 10%.")]
        [SerializeField] private Game.Data.TuningConfig _tuning;

        [Tooltip("Fuzzy mould spots, shown while the food is spoiled. Optional.")]
        [SerializeField] private GameObject _mould;

        [Tooltip("°C at which a layer starts to look cooked.")]
        [SerializeField] private float _cookedFrom = 40f;

        [Tooltip("Peak layer temperature (°C) → colour. Steak: red → pink → tan → grey.")]
        [SerializeField] private ColorStop[] _cooked =
        {
            new ColorStop(50f, new Color(0.78f, 0.20f, 0.22f)), // rare: bright red
            new ColorStop(55f, new Color(0.86f, 0.42f, 0.45f)), // medium-rare: pink
            new ColorStop(60f, new Color(0.78f, 0.56f, 0.50f)), // medium: rosy tan
            new ColorStop(66f, new Color(0.66f, 0.52f, 0.45f)), // medium-well: tan
            new ColorStop(72f, new Color(0.52f, 0.42f, 0.36f)), // well done: grey-brown
        };

        private MaterialPropertyBlock _block;
        private Color _surfaceRaw;
        private Color[] _bandRaw;
        private Ingredient _variedFor;
        private float _freshness = 1f;

        private float StaleBelow => _tuning != null ? _tuning.StaleBelow : 0.35f;
        private float SpoiledBelow => _tuning != null ? _tuning.SpoiledBelow : 0.1f;

        /// <summary>True for layered food (steak), whose middle band shows doneness.</summary>
        public bool ShowsDoneness => _layeredShader || _bands.Length >= 3;

        /// <summary>The middle band's colour this frame: the food's doneness at a glance.</summary>
        public Color CoreColor { get; private set; }

        private void Awake() => EnsureReady();

        private void EnsureReady()
        {
            if (_block != null)
                return;

            _block = new MaterialPropertyBlock();
            _surfaceRaw = _surface.sharedMaterial.GetColor(BaseColorId);
            _bandRaw = new Color[_bands.Length];
            for (var i = 0; i < _bands.Length; i++)
                _bandRaw[i] = _bands[i].sharedMaterial.GetColor(BaseColorId);
        }

        private void Update()
        {
            if (!(_itemView.Item is Ingredient ingredient))
                return;

            // Seeded from the ingredient rather than this view, so a piece keeps its look however
            // it moves between views. It's purely visual, so it doesn't touch the simulation.
            if (!ReferenceEquals(ingredient, _variedFor))
            {
                _variedFor = ingredient;
                ApplyVariation(RuntimeHelpers.GetHashCode(ingredient));
            }

            ShowFreshness(ingredient.Freshness);

            var burnt = Mathf.Max(ingredient.Properties.BurntBrowning, 0.0001f);
            // Layered food shows the side facing up, since it gets flipped. Loose food like diced
            // onion gets stirred, not flipped, so it shows its darkest side, or a burn would never show.
            var browning = (ShowsDoneness
                ? ingredient.Browning(ingredient.UpSide)
                : Mathf.Max(ingredient.Browning(IngredientSide.A), ingredient.Browning(IngredientSide.B))) / burnt;

            ShowState(browning, ingredient.Browning(ingredient.DownSide) / burnt,
                ingredient.PeakSurfaceTemperature(ingredient.DownSide),
                ingredient.PeakCoreTemperature, ingredient.PeakSurfaceTemperature(ingredient.UpSide));
        }

        /// <summary>
        /// Gives layered food its own fat pattern, size and angle. The same seed always gives the
        /// same look. Does nothing for other food.
        /// </summary>
        public void ApplyVariation(int seed)
        {
            if (!_layeredShader)
                return;

            EnsureReady();
            _surface.GetPropertyBlock(_block);
            _block.SetFloat(SeedId, Hash01(seed, 1));
            _surface.SetPropertyBlock(_block);

            _surface.transform.localRotation = Quaternion.Euler(0f, Hash01(seed, 2) * 360f, 0f);
            _surface.transform.localScale = Vector3.one * Mathf.Lerp(_sizeVariation.x, _sizeVariation.y, Hash01(seed, 3));
        }

        // Knuth's multiplicative hash, spreading similar seeds across 0..1; each channel gives an
        // independent value, so size doesn't follow pattern.
        private static float Hash01(int seed, uint channel)
        {
            unchecked
            {
                var hash = ((uint)seed + channel * 40503u) * 2654435761u;
                hash ^= hash >> 15;
                hash *= 2654435761u;
                return (hash >> 8) / 16777216f;
            }
        }

        /// <summary>
        /// How fresh the food is (1 fresh, 0 spoiled), for the colours <see cref="ShowState"/> draws
        /// next and the mould spots. Public so it can also be previewed outside Play mode.
        /// </summary>
        public void ShowFreshness(float freshness)
        {
            _freshness = freshness;
            if (_mould != null)
            {
                var mouldy = FreshnessLook.StageOf(freshness, StaleBelow, SpoiledBelow) == FreshnessLook.Stage.Mouldy;
                if (_mould.activeSelf != mouldy)
                    _mould.SetActive(mouldy);
            }
        }

        private Color Aged(Color colour) => FreshnessLook.Tint(colour, _freshness, StaleBelow, SpoiledBelow);

        /// <summary>
        /// Colours the food for a cooking state, with both sides browned alike: browning as a
        /// fraction of burnt, and each layer's peak temperature in °C. Public so it can also be
        /// previewed outside Play mode.
        /// </summary>
        public void ShowState(float browning, float bottomPeak, float corePeak, float topPeak) =>
            ShowState(browning, browning, bottomPeak, corePeak, topPeak);

        /// <summary>As above, with the top and the underside browned separately.</summary>
        public void ShowState(float browning, float bottomBrowning, float bottomPeak, float corePeak, float topPeak)
        {
            EnsureReady();
            var surface = ColorStop.Sample(_browning, browning, _surfaceRaw, 0f);

            if (_layeredShader)
            {
                // Before it browns, the top dulls from red as heat rises through the meat, as a
                // real steak's does, so cooking shows even before the first flip.
                surface = ColorStop.Sample(_browning, browning, Cooked(topPeak, _surfaceRaw), 0f);
                CoreColor = Cooked(corePeak, _surfaceRaw);
                _surface.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, Aged(surface));
                _block.SetFloat(BrowningId, browning);
                // The underside's crust shows as a seared line along the bottom of the edge.
                _block.SetColor(CrustBottomId, Aged(ColorStop.Sample(_browning, bottomBrowning, Cooked(bottomPeak, _surfaceRaw), 0f)));
                _block.SetFloat(BrowningBottomId, bottomBrowning);
                _block.SetColor(BandBottomId, Aged(Cooked(bottomPeak, _surfaceRaw)));
                _block.SetColor(BandCoreId, Aged(CoreColor));
                _block.SetColor(BandTopId, Aged(Cooked(topPeak, _surfaceRaw)));
                _surface.SetPropertyBlock(_block);
                return;
            }

            SetColor(_surface, Aged(surface));
            if (_bands.Length < 3)
                return;

            SetColor(_bands[0], Aged(Cooked(bottomPeak, _bandRaw[0])));
            CoreColor = Cooked(corePeak, _bandRaw[1]);
            SetColor(_bands[1], Aged(CoreColor));
            SetColor(_bands[2], Aged(Cooked(topPeak, _bandRaw[2])));
        }

        private Color Cooked(float peakTemperature, Color raw) => ColorStop.Sample(_cooked, peakTemperature, raw, _cookedFrom);

        private void SetColor(Renderer target, Color color)
        {
            target.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_block);
        }
    }
}
