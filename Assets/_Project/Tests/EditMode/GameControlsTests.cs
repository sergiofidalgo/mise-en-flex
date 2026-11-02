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
        public void HasTheFourActionMaps()
        {
            var maps = _controls.actionMaps.Select(m => m.name).ToArray();
            Assert.That(maps, Is.EquivalentTo(new[] { "Gameplay", "CloseUp", "Build", "UI" }));
        }

        [Test]
        public void GameplayMapHasPhase0Actions()
        {
            var actions = _controls.FindActionMap("Gameplay", true).actions.Select(a => a.name);
            Assert.That(actions, Is.SupersetOf(new[] { "Move", "Interact", "Use", "Pause", "Join" }));
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
