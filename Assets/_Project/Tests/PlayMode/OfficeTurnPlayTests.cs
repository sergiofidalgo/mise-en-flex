using System.Collections;
using Game.Gameplay;
using Game.Gameplay.Stations;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>Holding X plants the chef in the office too: the stick turns them on the spot (the user's call, 2026-11-11).</summary>
    public class OfficeTurnPlayTests
    {
        private Gamepad _pad;
        private InputSettings.BackgroundBehavior _background;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInput;

        [TearDown]
        public void Release()
        {
            if (_pad != null)
                InputSystem.RemoveDevice(_pad);
            _pad = null;
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
        }

        [UnityTest]
        public IEnumerator HoldingXInTheOfficeTurnsTheChefOnTheSpot()
        {
            _background = InputSystem.settings.backgroundBehavior;
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // The office is built by Tools/Game/Build Office and not committed: skip in a checkout without it.
            Assume.That(Application.CanStreamedLevelBeLoaded("Office_Greybox"), "build the office first (Tools/Game/Build Office)");
            yield return SceneManager.LoadSceneAsync("Office_Greybox");
            yield return null;
            _pad = InputSystem.AddDevice<Gamepad>("OfficeTurnPad");
            var player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Gamepad", pairWithDevices: _pad);
            for (var i = 0; i < 10; i++)
                yield return null;
            var chef = player.GetComponent<PlayerInteractor>();
            var movement = player.GetComponent<PlayerMovement>();
            Assume.That(chef.IsBusy, Is.False);

            // Let any arrival settle, then push the stick the way the chef isn't facing, X held.
            var settle = Time.time + 1.5f;
            while (Time.time < settle)
                yield return null;
            var start = player.transform.position;
            var back = player.transform.eulerAngles.y + 180f;
            var away = new Vector2(Mathf.Sin(back * Mathf.Deg2Rad), Mathf.Cos(back * Mathf.Deg2Rad));
            var until = Time.time + 0.8f;
            while (Time.time < until)
            {
                InputSystem.QueueStateEvent(_pad, new GamepadState { leftStick = away }.WithButton(GamepadButton.West));
                yield return null;
            }
            var moved = player.transform.position - start;
            moved.y = 0f;
            Assert.That(chef.IsPlanted, Is.True, "X held in the office plants the chef");
            Assert.That(moved.magnitude, Is.LessThan(0.05f), "planted, not walking");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, back)), Is.LessThan(10f), "turned on the spot");
            InputSystem.QueueStateEvent(_pad, new GamepadState());
            yield return null;
            Assert.That(movement, Is.Not.Null);
        }
    }
}
