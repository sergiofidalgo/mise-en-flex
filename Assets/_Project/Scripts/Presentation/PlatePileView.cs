using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows what's at the plate stack, the dirty pile, the sink or a rack as piles of discs, so running out and a
    /// growing pile of washing-up read at a glance. More plates than discs just shows a full pile.
    /// </summary>
    public sealed class PlatePileView : MonoBehaviour
    {
        [Tooltip("The plate stack or sink this pile belongs to.")]
        [SerializeField] private Interactable _station;

        [Tooltip("Clean plates, bottom first.")]
        [SerializeField] private GameObject[] _clean = System.Array.Empty<GameObject>();
        [Tooltip("The plate being washed (the sink only).")]
        [SerializeField] private GameObject[] _dirty = System.Array.Empty<GameObject>();

        private void Update()
        {
            if (_station is PlateStackStation stack && stack.Stack != null)
                Show(_clean, stack.Stack.Count);
            else if (_station is RackStation rack && rack.Rack != null)
                Show(_clean, rack.Rack.Count);
            else if (_station is SinkStation sink && sink.Sink != null)
                Show(_dirty, sink.Sink.DirtyCount);
        }

        private static void Show(GameObject[] discs, int count)
        {
            for (var i = 0; i < discs.Length; i++)
                discs[i].SetActive(i < count);
        }
    }
}
