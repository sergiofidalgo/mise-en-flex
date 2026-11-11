using System.Reflection;
using Game.Gameplay.Display;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Plays in full screen inside the Editor (decision 2026-11-11), where Unity's own full-screen
    /// calls do nothing: a second Game view with no toolbar and no frame, covering the display the
    /// Editor is on. Tools/Game/Play Full Screen, the Display row in Settings or Ctrl+Cmd+F open and
    /// close it; leaving Play mode closes it too. Esc still pauses, as everywhere else.
    ///
    /// While it's open it is kept as Unity's "main" play mode view, the one whose size
    /// <see cref="Screen"/> reports and the HUD is laid out for; otherwise the HUD keeps the docked
    /// Game view's size and lands short of the right and bottom edges (seen 2026-11-11).
    ///
    /// Unity has no public API for a Game view like this, so it uses the internal GameView class, its
    /// showToolbar property and PlayModeView.s_LastFocused. If a Unity upgrade renames them, the menu item says so in the Console
    /// and the game is unaffected; this is an Editor tool only.
    /// </summary>
    [InitializeOnLoad]
    internal static class FullScreenPlay
    {
        private const string MenuPath = "Tools/Game/Play Full Screen";

        private static EditorWindow _view;
        private static FieldInfo _lastFocused;

        static FullScreenPlay()
        {
            WindowMode.EditorTarget = new Target();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                    Close();
            };
        }

        private static bool IsOpen => _view != null;

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanToggle()
        {
            Menu.SetChecked(MenuPath, IsOpen);
            return EditorApplication.isPlaying;
        }

        private static void Open()
        {
            if (IsOpen || !EditorApplication.isPlaying)
                return;
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (type == null)
            {
                Debug.LogWarning("Play Full Screen: this Unity version has no UnityEditor.GameView class; the tool needs updating.");
                return;
            }
            _view = (EditorWindow)ScriptableObject.CreateInstance(type);
            _view.titleContent = new GUIContent("Full screen");
            var toolbar = type.GetProperty("showToolbar", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (toolbar != null && toolbar.CanWrite)
                toolbar.SetValue(_view, false);
            else
                Debug.LogWarning("Play Full Screen: couldn't hide the Game view's toolbar in this Unity version.");

            var display = DisplayRect();
            _view.ShowPopup();
            _view.position = display;
            // Pinned, so nothing in the Editor resizes the popup while it covers the display.
            _view.minSize = _view.maxSize = display.size;
            _view.Focus();

            _lastFocused = typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView")?
                .GetField("s_LastFocused", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (_lastFocused == null)
                Debug.LogWarning("Play Full Screen: couldn't make the full-screen view the main one in this Unity version; the HUD may be laid out for the docked Game view.");
            KeepMain();
            EditorApplication.update += KeepMain;
        }

        // Unity takes "the last focused play mode view" as the main one, and focus can drift back to the
        // docked Game view (a click, a window coming forward), so it's set again every Editor update.
        private static void KeepMain()
        {
            if (!IsOpen || _lastFocused == null)
                return;
            if (!ReferenceEquals(_lastFocused.GetValue(null), _view))
                _lastFocused.SetValue(null, _view);
        }

        private static void Close()
        {
            if (!IsOpen)
                return;
            EditorApplication.update -= KeepMain;
            _view.Close();
            _view = null;
            // Back to the docked Game view, so the game keeps the keyboard and controllers.
            if (EditorApplication.isPlaying)
                EditorApplication.ExecuteMenuItem("Window/General/Game");
        }

        // The usable part of the display the Editor is on (below the menu bar, which macOS keeps on top
        // anyway), in points: window positions are in points, the display's sizes in pixels, twice as
        // many on a Retina screen. Unity doesn't give the display's corner in window coordinates, so
        // it's taken from the main window's left and bottom edges, exact when the Editor is maximized
        // (seen 2026-11-11: main window 58,-1022 1920x1022 on a 1920x1080 display with a 30 px menu bar).
        private static Rect DisplayRect()
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            var work = Screen.mainWindowDisplayInfo.workArea;
            var scale = EditorGUIUtility.pixelsPerPoint;
            var size = work.width > 0 && work.height > 0
                ? new Vector2(work.width / scale, work.height / scale)
                : new Vector2(Screen.currentResolution.width / scale, Screen.currentResolution.height / scale);
            return new Rect(main.x, main.yMax - size.y, size.x, size.y);
        }

        private sealed class Target : WindowMode.ITarget
        {
            public bool IsFullScreen => FullScreenPlay.IsOpen;

            public void Set(bool fullScreen)
            {
                if (fullScreen)
                    Open();
                else
                    Close();
            }
        }
    }
}
