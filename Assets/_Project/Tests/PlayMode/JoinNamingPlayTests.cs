using System.Collections;
using System.IO;
using Game.Gameplay;
using Game.Gameplay.Saving;
using Game.Gameplay.Stations;
using Game.Simulation.Days;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Naming yourself as you join a kitchen, and being told to wait during service (the user's
    /// calls, 2026-11-13). Saves go to a throwaway folder, so the real chefs are never touched.
    /// </summary>
    public class JoinNamingPlayTests
    {
        private const string SceneName = "Kitchen_Greybox";

        private Gamepad _pad;
        private Gamepad _first;
        private string _saves;
        private InputSettings.BackgroundBehavior _background;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInput;

        [SetUp]
        public void Isolate()
        {
            JoinNaming.Enabled = true;
            _saves = Path.Combine(Path.GetTempPath(), "JoinNamingPlayTests");
            if (Directory.Exists(_saves))
                Directory.Delete(_saves, true);
            Directory.CreateDirectory(_saves);
            SaveFile.DirectoryOverride = _saves;
            ChefProfileFile.Reset();
            _background = InputSystem.settings.backgroundBehavior;
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [TearDown]
        public void Restore()
        {
            JoinNaming.Enabled = false; // the suite's setting (PlayModeSetup)
            if (_pad != null)
                InputSystem.RemoveDevice(_pad);
            _pad = null;
            if (_first != null)
                InputSystem.RemoveDevice(_first);
            _first = null;
            SaveFile.DirectoryOverride = null;
            ChefProfileFile.Reset();
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
        }

        [UnityTest]
        public IEnumerator ANewPlayerStandsStillUntilTheyHaveAName()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            // Last time's name, one press keeps it: in every slot, as players carried over from
            // earlier tests can hold the first ones.
            for (var slot = 0; slot < 4; slot++)
                ChefProfileFile.Current.SetName(slot, "Rosa");
            var naming = Object.FindFirstObjectByType<JoinNaming>();
            Assert.That(naming, Is.Not.Null, "the kitchen has naming on join");
            var arrived = 0;
            naming.Arrived += _ => arrived++;

            _pad = InputSystem.AddDevice<Gamepad>("NamingTestPad");
            var player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Gamepad", pairWithDevices: _pad);
            yield return null;
            yield return null;
            var hands = player.GetComponent<PlayerInteractor>();
            Assert.That(naming.IsNaming(player), Is.True);
            Assert.That(hands.IsBusy, Is.True, "held still while naming");
            Assert.That(naming.For(player.playerIndex).Picker.Text, Is.Empty);
            Assert.That(naming.For(player.playerIndex).Picker.Selected.Kind, Is.EqualTo(Game.Simulation.Campaign.NameKeyKind.Keep), "the cursor starts on last time's name");

            // A on the top key keeps last time's name.
            // Held as long as a real press (about a tenth of a second): a single frame can slip
            // between input updates in batch mode, which made this flaky.
            Press(_pad.buttonSouth);
            yield return new WaitForSecondsRealtime(0.1f);
            Release(_pad.buttonSouth);
            yield return null;
            yield return null;
            Assert.That(naming.IsNaming(player), Is.False, "A on last time's name finishes");
            Assert.That(hands.IsBusy, Is.False, "free to cook");
            Assert.That(arrived, Is.EqualTo(1));
            Assert.That(ChefProfileFile.Current.Name(player.playerIndex), Is.EqualTo("Rosa"));
        }

        [UnityTest]
        public IEnumerator NobodyNewJoinsDuringService()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var gate = Object.FindFirstObjectByType<KitchenJoinGate>();
            Assert.That(gate, Is.Not.Null);
            // Someone's already cooking: an empty kitchen would take its first player any time.
            _first = InputSystem.AddDevice<Gamepad>("CookingPad");
            Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Gamepad", pairWithDevices: _first);
            yield return null;
            var days = root.Services.Days;
            while (days.Phase != DayPhase.Service)
                days.Advance();
            var refused = 0;
            gate.Refused += _ => refused++;
            yield return null;
            var manager = Object.FindFirstObjectByType<PlayerInputManager>();
            Assert.That(manager.joiningEnabled, Is.False, "joining closes for the service");

            _pad = InputSystem.AddDevice<Gamepad>("WaitingPad");
            Press(_pad.buttonSouth);
            yield return new WaitForSecondsRealtime(0.1f);
            Release(_pad.buttonSouth);
            yield return null;
            Assert.That(refused, Is.GreaterThan(0), "they're told to wait");
            Assert.That(manager.playerCount, Is.EqualTo(1), "only the cook who was already in");
        }

        private static void Press(UnityEngine.InputSystem.Controls.ButtonControl button) =>
            InputSystem.QueueStateEvent(button.device as Gamepad, State(button, true));

        private static void Release(UnityEngine.InputSystem.Controls.ButtonControl button) =>
            InputSystem.QueueStateEvent(button.device as Gamepad, State(button, false));

        private static UnityEngine.InputSystem.LowLevel.GamepadState State(UnityEngine.InputSystem.Controls.ButtonControl button, bool down)
        {
            var state = new UnityEngine.InputSystem.LowLevel.GamepadState();
            if (down)
                state = state.WithButton(UnityEngine.InputSystem.LowLevel.GamepadButton.South);
            return state;
        }
    }
}
