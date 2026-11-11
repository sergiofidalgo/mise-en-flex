using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>
    /// The developer toggle for the kitchen's debug panels (the cooking overlay, the quality card):
    /// off by default, so players see only what's built into the scene (the user's call, 2026-11-11
    /// playtest round). F1 flips it; it's a debug key, deliberately outside the rebindable controls.
    /// In the Editor the choice is remembered between Play sessions; a build always starts with it off.
    /// </summary>
    public static class DeveloperHud
    {
        private const string PrefsKey = "Game.DeveloperHud.Visible";
        private static bool? _visible;
        private static int _checkedFrame = -1;

        /// <summary>Whether the debug panels show. Setting it in the Editor remembers it.</summary>
        public static bool Visible
        {
            get
            {
                if (_visible == null)
                    _visible = Application.isEditor && PlayerPrefs.GetInt(PrefsKey, 0) == 1;
                return _visible.Value;
            }
            set
            {
                _visible = value;
                if (Application.isEditor)
                    PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0);
            }
        }

        /// <summary>Reads F1 once a frame, however many panels call it.</summary>
        public static void ReadToggle()
        {
            if (_checkedFrame == Time.frameCount)
                return;
            _checkedFrame = Time.frameCount;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
                Visible = !Visible;
        }
    }
}
