using System;
using System.Linq;
using Game.Simulation.Campaign;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// One restaurant on the city miniature (Phase 6, decision 2026-11-10): what the lot is before
    /// anyone buys it. The builder makes one kitchen scene from each definition (a later milestone);
    /// the save keeps what players did with it, keyed by <see cref="Id"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Restaurant Definition", fileName = "RES_Restaurant")]
    public sealed class RestaurantDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class OldReviewEntry
        {
            [SerializeField, Range(0f, 5f)] private float _stars = 3f;
            [SerializeField] private string _line = "";
            [Tooltip("The guest's handle, e.g. \"Mike S.\".")]
            [SerializeField] private string _guest = "";

            public OldReviewEntry() { }

            public OldReviewEntry(float stars, string line, string guest)
            {
                _stars = stars;
                _line = line;
                _guest = guest;
            }

            public OldReview ToSettings() => new OldReview { Stars = _stars, Line = _line, Guest = _guest };
        }

        [Tooltip("Stable id kept in saves. Never change it once players have saves that own this restaurant.")]
        [SerializeField] private string _id = "";
        [Tooltip("The name it trades under when bought; players may keep it (with its reviews) or rename it.")]
        [SerializeField] private string _displayName = "";

        [Header("Size")]
        [SerializeField] private RestaurantSize _size = RestaurantSize.Counter;
        [Tooltip("Guests seated at once with the furniture it comes with.")]
        [SerializeField, Min(0)] private int _seats;

        [Header("Money")]
        [Tooltip("What buying it costs on the miniature.")]
        [SerializeField, Min(0f)] private float _price;
        [Tooltip("Times the base fee in the Service Config: a bigger place on a better street owes the boss more.")]
        [SerializeField, Min(0f)] private float _feeScale = 1f;

        [Header("What it comes with")]
        [Tooltip("Furniture catalogue ids left by the previous owner.")]
        [SerializeField] private string[] _inheritedAppliances = Array.Empty<string>();
        [Tooltip("Its style before it was bought; the first grill's is fixed.")]
        [SerializeField] private string _previousStyle = "";
        [Tooltip("Players choose only its name when buying, not its style: the first grill (game-design §15.2).")]
        [SerializeField] private bool _fixedStyle;
        [Tooltip("Reviews from the previous owner, kept if players keep the name.")]
        [SerializeField] private OldReviewEntry[] _oldReviews = Array.Empty<OldReviewEntry>();

        [Header("Street")]
        [Tooltip("The street it's on, also the area reviews and the slot card name.")]
        [SerializeField] private string _street = "";
        [Tooltip("The stalls on its street, e.g. \"Butcher\".")]
        [SerializeField] private string[] _vendors = Array.Empty<string>();

        public string Id => _id;
        public string DisplayName => _displayName;
        public float Price => _price;

        /// <summary>For the builder: fills a new definition in one go.</summary>
        public void Set(string id, string displayName, RestaurantSize size, int seats, float price, float feeScale,
            string[] inheritedAppliances, string previousStyle, OldReviewEntry[] oldReviews, string street, string[] vendors, bool fixedStyle = false)
        {
            _fixedStyle = fixedStyle;
            _id = id;
            _displayName = displayName;
            _size = size;
            _seats = seats;
            _price = price;
            _feeScale = feeScale;
            _inheritedAppliances = inheritedAppliances ?? Array.Empty<string>();
            _previousStyle = previousStyle;
            _oldReviews = oldReviews ?? Array.Empty<OldReviewEntry>();
            _street = street;
            _vendors = vendors ?? Array.Empty<string>();
        }

        public RestaurantSettings ToSettings() => new RestaurantSettings
        {
            Id = _id ?? "",
            Name = _displayName ?? "",
            Size = _size,
            Seats = _seats,
            Price = _price,
            FeeScale = _feeScale,
            InheritedAppliances = (_inheritedAppliances ?? Array.Empty<string>()).ToArray(),
            PreviousStyle = _previousStyle ?? "",
            FixedStyle = _fixedStyle,
            OldReviews = (_oldReviews ?? Array.Empty<OldReviewEntry>()).Where(r => r != null).Select(r => r.ToSettings()).ToArray(),
            Street = _street ?? "",
            Vendors = (_vendors ?? Array.Empty<string>()).ToArray(),
        };
    }
}
