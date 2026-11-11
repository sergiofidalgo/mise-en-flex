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
    /// Unity has no public API for a Game view like this, so it uses the internal GameView class and
    /// its showToolbar property. If a Unity upgrade renames them, the menu item says so in the Console
    /// and the game is unaffected; this is an Editor tool only.
    /// </summary>
    [InitializeOnLoad]
    internal static class FullScreenPlay
    {
        private const string MenuPath = "Tools/Game/Play Full Screen";

        private static EditorWindow _view;

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
        }

        private static void Close()
        {
            if (!IsOpen)
                return;
            _view.Close();
            _view = null;
            // Back to the docked Game view, so the game keeps the keyboard and controllers.
            if (EditorApplication.isPlaying)
                EditorApplication.ExecuteMenuItem("Window/General/Game");
        }

        // The display the Editor's main window is on, in points (window positions are in points; the
        // display's resolution is in pixels, twice as many on a Retina screen).
        private static Rect DisplayRect()
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            var resolution = Screen.currentResolution;
            var scale = EditorGUIUtility.pixelsPerPoint;
            var size = new Vector2(resolution.width / scale, resolution.height / scale);
            // The main window's corner tells which display it is on, for a second monitor laid out
            // beside the first; a display of the same size is assumed.
            var origin = new Vector2(Mathf.Floor(main.x / size.x) * size.x, Mathf.Floor(main.y / size.y) * size.y);
            return new Rect(origin, size);
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
