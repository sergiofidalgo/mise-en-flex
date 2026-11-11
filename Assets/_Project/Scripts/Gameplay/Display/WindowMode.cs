using UnityEngine;

namespace Game.Gameplay.Display
{
    /// <summary>
    /// Full screen or a window (decision 2026-11-11). In a built game this is Unity's borderless
    /// full-screen window, which suits macOS best (no display mode switch, Cmd+Tab still works). In the
    /// Editor <see cref="Screen"/> does nothing, so the Editor's full-screen Game view (Game.Editor's
    /// FullScreenPlay) registers itself as <see cref="EditorTarget"/> and stands in for it.
    /// </summary>
    public static class WindowMode
    {
        /// <summary>What switches full screen in the Editor; set by the Editor assembly, null in a build.</summary>
        public interface ITarget
        {
            bool IsFullScreen { get; }
            void Set(bool fullScreen);
        }

        // The windowed size, as a share of the display: big enough to play, small enough to leave the
        // Dock and the menu bar clear. Kept at the display's own shape so nothing is letterboxed.
        private const float WindowShare = 0.75f;

        public static ITarget EditorTarget { get; set; }

        // Screen.fullScreenMode only changes on the next frame, so the last request is remembered for a
        // settings row that redraws straight after the change.
        private static bool? _requested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _requested = null;

        public static bool IsFullScreen =>
            EditorTarget?.IsFullScreen ?? _requested ?? Screen.fullScreenMode != FullScreenMode.Windowed;

        public static void Set(bool fullScreen)
        {
            if (Application.isEditor)
            {
                EditorTarget?.Set(fullScreen);
                return;
            }
            _requested = fullScreen;
            var display = Screen.currentResolution;
            if (fullScreen)
                Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow);
            else
                Screen.SetResolution(Mathf.RoundToInt(display.width * WindowShare), Mathf.RoundToInt(display.height * WindowShare), FullScreenMode.Windowed);
        }

        public static void Toggle() => Set(!IsFullScreen);
    }
}
