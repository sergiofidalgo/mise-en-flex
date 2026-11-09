using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.Office
{
    /// <summary>
    /// The city miniature in the boss's back room, the way out of the office (decision 2026-11-09,
    /// simple miniature): once every joined player stands around the table, any of them presses A
    /// and the day starts in the kitchen, with the same players on the same controllers
    /// (PlayerRoster). The campaign was already chosen in the menu (CampaignLaunch). Choosing among
    /// restaurants on the miniature comes with the campaign (Phase 6).
    /// </summary>
    public sealed class MiniatureTable : MonoBehaviour
    {
        private const string InteractAction = "Interact";

        [Tooltip("Half the size of the area around the table that counts as at it (m, local X and Z): the table plus an arm's reach.")]
        [SerializeField] private Vector2 _halfArea = new Vector2(3.4f, 2.5f);
        [SerializeField] private string _kitchenScene = "Kitchen_Greybox";

        /// <summary>Raised just before the kitchen loads, for feedback.</summary>
        public event System.Action Leaving;

        /// <summary>How many joined players are at the table.</summary>
        public int AtTable { get; private set; }

        /// <summary>How many players have joined.</summary>
        public int Joined => PlayerInput.all.Count;

        /// <summary>Everyone who has joined is at the table.</summary>
        public bool Ready => Joined > 0 && AtTable == Joined;

        private bool _leaving;

        private void Update()
        {
            AtTable = 0;
            foreach (var player in PlayerInput.all)
                if (IsAt(player.transform.position))
                    AtTable++;
            if (!Ready || _leaving)
                return;
            foreach (var player in PlayerInput.all)
            {
                if (!player.actions[InteractAction].WasPressedThisFrame())
                    continue;
                _leaving = true;
                Leaving?.Invoke();
                PlayerRoster.CarryEveryone(); // the same players walk into the kitchen
                SceneManager.LoadScene(_kitchenScene);
                return;
            }
        }

        private bool IsAt(Vector3 world)
        {
            var local = transform.InverseTransformPoint(world);
            return Mathf.Abs(local.x) <= _halfArea.x && Mathf.Abs(local.z) <= _halfArea.y;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up * 0.5f, new Vector3(_halfArea.x * 2f, 1f, _halfArea.y * 2f));
        }
    }
}
