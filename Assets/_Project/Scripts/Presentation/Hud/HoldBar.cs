using System.Linq;
using System.Text;
using Game.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The fill shown while a player holds Menu to ready up (decision 2026-11-10), as rich text, so
    /// any text HUD can carry it: ten bars filling in the player's colour. Placeholder look until the
    /// UI pass draws a ring.
    /// </summary>
    public static class HoldBar
    {
        private const int Steps = 10;

        /// <summary>The player holding Menu furthest, or null with nobody holding.</summary>
        public static PlayerInput Holder() =>
            PlayerInput.all.Where(p => MenuButton.HoldProgress(p) > 0f).OrderByDescending(MenuButton.HoldProgress).FirstOrDefault();

        /// <summary>Filled steps of the holder's bar (0 with nobody holding), for a HUD's redraw key.</summary>
        public static int Filled(PlayerInput holder) =>
            holder != null ? Mathf.Clamp(Mathf.CeilToInt(MenuButton.HoldProgress(holder) * Steps), 0, Steps) : 0;

        public static string Text(PlayerInput holder, Color colour, Color empty)
        {
            var filled = Filled(holder);
            if (filled == 0)
                return "";
            var text = new StringBuilder("<cspace=-0.05em>");
            text.Append(HudStyle.Colored(new string('|', filled), colour));
            text.Append(HudStyle.Colored(new string('|', Steps - filled), empty));
            text.Append("</cspace>");
            return text.ToString();
        }
    }
}
