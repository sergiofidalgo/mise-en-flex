using Game.Gameplay.Saving;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Display
{
    /// <summary>
    /// Puts a built game in the window mode the settings saved, and listens for Ctrl+Cmd+F, the Mac's
    /// own full-screen shortcut, in every scene (decision 2026-11-11). It's a keyboard extra for
    /// testing: the Display row in Settings is the way in for controllers, so no hint names the keys.
    /// Created once at startup and kept across scenes, because it belongs to no one scene.
    /// </summary>
    public sealed class WindowModeKeys : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var keys = new GameObject(nameof(WindowModeKeys)).AddComponent<WindowModeKeys>();
            keys.gameObject.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(keys.gameObject);

            // The Editor opens in its docked Game view; only a build starts the way it was left.
            if (!Application.isEditor)
                WindowMode.Set(PlayerSettings.Load(null).FullScreen);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.fKey.wasPressedThisFrame)
                return;
            if (!keyboard.ctrlKey.isPressed || !(keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed))
                return;

            WindowMode.Toggle();
            // The Editor's full-screen Game view isn't saved, so playing there never changes how the
            // built game starts.
            if (Application.isEditor)
                return;
            var settings = PlayerSettings.Load(null);
            settings.FullScreen = WindowMode.IsFullScreen;
            settings.Save();
        }
    }
}
