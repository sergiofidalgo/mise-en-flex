using System.Text;
using Game.Gameplay;
using Game.Simulation.Cooking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>
    /// Developer overlay with the numbers behind cooking: game clock, hob dials, pan
    /// temperatures and every ingredient's temperatures, doneness, browning and freshness.
    /// F1 toggles it. Players read food by eye; this is for tuning.
    /// </summary>
    public sealed class CookingDebugOverlay : MonoBehaviour
    {
        private const float Margin = 10f;
        private const float Width = 560f;

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private bool _visible = true;

        private readonly StringBuilder _text = new StringBuilder();
        private GUIStyle _style;

        private void Update()
        {
            // Debug-only shortcut, deliberately outside the rebindable game controls.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
                _visible = !_visible;
        }

        private void OnGUI()
        {
            var cooking = _root != null ? _root.Services?.Cooking : null;
            if (!_visible || cooking == null)
                return;

            _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, fontSize = 12 };

            _text.Clear();
            var minutes = (int)(cooking.ElapsedSeconds / 60.0);
            _text.AppendLine($"<b>Cooking</b>  (F1 hides)   game time {minutes / 60:00}:{minutes % 60:00} since start");

            for (var i = 0; i < cooking.Hobs.Count; i++)
            {
                var hob = cooking.Hobs[i];
                _text.AppendLine($"<b>Hob {i + 1}</b>  dial {hob.Dial:P0}  {(hob.Pan != null ? "pan on" : "no pan")}");
            }

            for (var i = 0; i < cooking.Pans.Count; i++)
            {
                var pan = cooking.Pans[i];
                var onHob = -1;
                for (var h = 0; h < cooking.Hobs.Count; h++)
                {
                    if (cooking.Hobs[h].Pan == pan)
                        onHob = h;
                }
                _text.AppendLine($"<b>Pan {i + 1}</b>  {pan.Temperature:0} °C  {(onHob >= 0 ? $"on hob {onHob + 1}" : "off the heat")}");
                foreach (var ingredient in pan.Contents)
                    AppendIngredient(ingredient, "   ");
            }

            // Per-player input: which device, what they're facing, and the live heat trigger values.
            foreach (var player in PlayerInput.all)
            {
                var devices = string.Join(", ", System.Linq.Enumerable.Select(player.devices, d => d.displayName));
                var target = player.TryGetComponent(out Game.Gameplay.Stations.PlayerInteractor interactor) && interactor.Target != null
                    ? interactor.Target.name
                    : "nothing";
                var down = player.actions.FindAction("HeatDown")?.ReadValue<float>() ?? -1f;
                var up = player.actions.FindAction("HeatUp")?.ReadValue<float>() ?? -1f;
                _text.AppendLine($"<b>P{player.playerIndex + 1}</b> [{devices}]  facing {target}  heat down {down:0.00} / up {up:0.00}");
            }

            var loose = 0;
            foreach (var ingredient in cooking.Ingredients)
            {
                if (ingredient.Pan != null)
                    continue;
                if (loose++ == 0)
                    _text.AppendLine("<b>Out of the pan</b>");
                AppendIngredient(ingredient, "   ");
            }

            var content = new GUIContent(_text.ToString());
            var height = _style.CalcHeight(content, Width);
            GUI.Box(new Rect(Margin, Margin, Width, height), content, _style);
        }

        private void AppendIngredient(Ingredient ingredient, string indent)
        {
            var doneness = ingredient.Doneness?.Name ?? "raw";
            var down = ingredient.DownSide;
            var burnt = ingredient.IsBurnt ? " <color=#ff6040>BURNT</color>" : string.Empty;
            _text.AppendLine(
                $"{indent}{ingredient.TypeId}: core {ingredient.CoreTemperature:0} °C (peak {ingredient.PeakCoreTemperature:0}), " +
                $"surfaces A {ingredient.SurfaceTemperature(IngredientSide.A):0} / B {ingredient.SurfaceTemperature(IngredientSide.B):0}, " +
                $"down {down}  |  <b>{doneness}</b>  |  browning A {ingredient.Browning(IngredientSide.A):0.00} / " +
                $"B {ingredient.Browning(IngredientSide.B):0.00}{burnt}  |  fresh {ingredient.Freshness:P0}");
        }
    }
}
