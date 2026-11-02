using System.Collections.Generic;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// Names the doneness of the food each player is dealing with, in the same colours as the
    /// order tickets, so "is this what seat 2 asked for?" is a glance (decision 2026-11-02). The
    /// food is what they carry (on its own, in a pan or on a plate), else what's on the station
    /// they face. Food without doneness levels gets no tag.
    /// </summary>
    public sealed class DonenessTagHud : MonoBehaviour
    {
        private const string RawName = "Raw";

        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;

        [Tooltip("Where the tag hangs from the food, as a card pivot: y above 1 drops it below the food on screen, clear of the prompt card above the station.")]
        [SerializeField] private Vector2 _pivot = new Vector2(0.5f, 1.35f);
        [Tooltip("Canvas pixels of padding inside a tag.")]
        [SerializeField] private Vector2 _padding = new Vector2(10f, 4f);

        private readonly Dictionary<PlayerInput, WorldLabel> _tags = new Dictionary<PlayerInput, WorldLabel>();
        private readonly HashSet<Ingredient> _tagged = new HashSet<Ingredient>();
        private readonly Dictionary<string, string> _texts = new Dictionary<string, string>();

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null || _root == null || _root.Services == null)
                return;

            _tagged.Clear();
            foreach (var player in PlayerInput.all)
            {
                var tag = TagFor(player);
                var food = player.TryGetComponent(out PlayerInteractor interactor) ? FoodOf(interactor) : null;
                // Two players looking at the same steak share one tag.
                if (food == null || !_tagged.Add(food) || !_root.Services.ItemViews.TryGetView(food, out var view))
                {
                    tag.Hide();
                    continue;
                }

                tag.Show(TextFor(food.Doneness?.Name ?? RawName), view.transform.position, camera, _canvas);
            }
        }

        private static Ingredient FoodOf(PlayerInteractor player) =>
            MainFood(player.Hands.Current) ?? (player.Target != null ? MainFood(player.Target.HeldItem) : null);

        // The first food with doneness levels: the item itself, or inside a pan or on a plate.
        private static Ingredient MainFood(Item item)
        {
            if (item is Ingredient ingredient)
                return ingredient.Properties.DonenessLevels.Count > 0 ? ingredient : null;
            if (item is IItemCollection collection)
            {
                foreach (var inside in collection.Items)
                {
                    if (inside is Ingredient food && food.Properties.DonenessLevels.Count > 0)
                        return food;
                }
            }
            return null;
        }

        private string TextFor(string doneness)
        {
            if (!_texts.TryGetValue(doneness, out var text))
            {
                text = HudStyle.Colored(doneness, _style.DonenessColor(doneness));
                _texts[doneness] = text;
            }
            return text;
        }

        private WorldLabel TagFor(PlayerInput player)
        {
            if (_tags.TryGetValue(player, out var tag) && tag != null)
                return tag;
            tag = WorldLabel.Create(_style, _canvas, $"DonenessTag_P{player.playerIndex + 1}", _padding, _pivot);
            _tags[player] = tag;
            return tag;
        }
    }
}
