using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Tests.EditMode
{
    public class GameControlsTests
    {
        private const string ControlsPath = "Assets/_Project/Data/Input/GameControls.inputactions";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/PF_Player.prefab";
        private const string GamepadGroup = "Gamepad";
        private const string KeyboardGroup = "Keyboard&Mouse";

        private InputActionAsset _controls;

        [SetUp]
        public void LoadControls()
        {
            _controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);
            Assert.That(_controls, Is.Not.Null, $"Missing input actions at {ControlsPath}");
        }

        [Test]
        public void HasTheActionMaps()
        {
            var maps = _controls.actionMaps.Select(m => m.name).ToArray();
            Assert.That(maps, Is.EquivalentTo(new[] { "Gameplay", "Build", "UI" }));
        }

        [Test]
        public void GameplayMapHasPhase0Actions()
        {
            var actions = _controls.FindActionMap("Gameplay", true).actions.Select(a => a.name);
            Assert.That(actions, Is.SupersetOf(new[] { "Move", "Interact", "Use", "Pause", "Join", "HeatDown", "HeatUp", "ButtonB", "ButtonY" }));
        }

        // CLAUDE.md: "Every action has a gamepad and a keyboard binding."
        [Test]
        public void EveryActionHasGamepadAndKeyboardBindings()
        {
            foreach (var action in _controls.actionMaps.SelectMany(m => m.actions))
            {
                Assert.That(HasBindingInGroup(action, GamepadGroup), $"{action.actionMap.name}/{action.name} has no gamepad binding");
                Assert.That(HasBindingInGroup(action, KeyboardGroup), $"{action.actionMap.name}/{action.name} has no keyboard binding");
            }
        }

        // Decision 2026-11-02: Interact and Use are separate buttons, so putting
        // something down never enters a close-up by accident.
        [Test]
        public void InteractAndUseNeverShareABinding()
        {
            var gameplay = _controls.FindActionMap("Gameplay", true);
            var interact = gameplay.FindAction("Interact", true).bindings.Select(b => b.path);
            var use = gameplay.FindAction("Use", true).bindings.Select(b => b.path);
            Assert.That(interact.Intersect(use), Is.Empty);
        }

        // Decision 2026-11-02: the keyboard mirrors the face buttons as a diamond
        // (X = A, D = B, A = X, W = Y) and arrow keys move.
        [TestCase("Gameplay", "Interact", "<Keyboard>/x", "<Gamepad>/buttonSouth")]
        [TestCase("Gameplay", "Use", "<Keyboard>/a", "<Gamepad>/buttonWest")]
        [TestCase("Build", "Cancel", "<Keyboard>/d", "<Gamepad>/buttonEast")]
        [TestCase("Gameplay", "ButtonB", "<Keyboard>/d", "<Gamepad>/buttonEast")]
        [TestCase("Gameplay", "ButtonY", "<Keyboard>/w", "<Gamepad>/buttonNorth")]
        [TestCase("Gameplay", "HeatDown", "<Keyboard>/q", "<Gamepad>/leftTrigger")]
        [TestCase("Gameplay", "HeatUp", "<Keyboard>/e", "<Gamepad>/rightTrigger")]
        [TestCase("Gameplay", "Manage", "<Keyboard>/tab", "<Gamepad>/select")]
        public void KeyboardMirrorsFaceButtons(string map, string action, string key, string button)
        {
            var paths = _controls.FindActionMap(map, true).FindAction(action, true).bindings.Select(b => b.path).ToArray();
            Assert.That(paths, Does.Contain(key).And.Contain(button));
        }

        [Test]
        public void ArrowKeysMoveAndWasdDoesNot()
        {
            var paths = _controls.FindActionMap("Gameplay", true).FindAction("Move", true).bindings.Select(b => b.path).ToArray();
            Assert.That(paths, Does.Contain("<Keyboard>/upArrow"));
            Assert.That(paths, Does.Not.Contain("<Keyboard>/w"));
        }

        [Test]
        public void PlayerPrefabUsesGameControlsStartingInGameplay()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing player prefab at {PlayerPrefabPath}; run Tools/Game/Build Greybox Kitchen");

            var playerInput = prefab.GetComponent<PlayerInput>();
            Assert.That(playerInput, Is.Not.Null);
            Assert.That(playerInput.actions, Is.EqualTo(_controls));
            Assert.That(playerInput.defaultActionMap, Is.EqualTo("Gameplay"));
        }

        private static bool HasBindingInGroup(InputAction action, string group)
        {
            return action.bindings
                .Where(b => !b.isComposite)
                .Any(b => b.groups != null && b.groups.Split(InputBinding.Separator).Contains(group));
        }
    }
}
