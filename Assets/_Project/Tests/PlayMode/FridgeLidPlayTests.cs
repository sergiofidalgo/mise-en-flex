using System.Collections;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Stations;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>The chest fridge's lid (decision 2026-11-08): shut unless someone has its menu open.</summary>
    public class FridgeLidPlayTests
    {
        private static IEnumerator Wait(float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        [UnityTest]
        public IEnumerator TheFridgeLidStartsTheDayShut()
        {
            // The user's bug report (2026-11-08): the lid stood open from the start until someone
            // opened and closed it.
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            var keyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.GetDevice<Mouse>() ?? InputSystem.AddDevice<Mouse>();
            Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Keyboard&Mouse", pairWithDevices: new InputDevice[] { keyboard, mouse });
            yield return Wait(1.5f);

            var fridges = Object.FindObjectsByType<StorageStation>(FindObjectsSortMode.None)
                .Select(s => (Station: s, Door: s.GetComponents<MonoBehaviour>().FirstOrDefault(c => c.GetType().Name == "FridgeDoorView")))
                .Where(f => f.Door != null)
                .ToArray();
            Assert.That(fridges, Is.Not.Empty, "the kitchen has a fridge with a lid");
            foreach (var (station, door) in fridges)
            {
                Assert.That(door.GetType().GetProperty("IsOpen").GetValue(door), Is.False, $"{station.name} is shut");
                var lid = station.GetComponentsInChildren<Transform>(true).First(t => t.name == "Lid");
                Assert.That(Quaternion.Angle(lid.localRotation, Quaternion.identity), Is.LessThan(1f), $"{station.name}'s lid lies flat");
            }
        }
    }
}
