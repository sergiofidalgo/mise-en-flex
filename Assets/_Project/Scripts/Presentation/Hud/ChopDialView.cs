using Game.Simulation.Prep;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The cutting dial: a ring with the ingredient's sweet spot (amber edges for Early/Late, a
    /// green core for Good), a needle sweeping round, a dot per cut and the last grade. Press X as
    /// the needle crosses the green.
    /// </summary>
    public sealed class ChopDialView : MonoBehaviour
    {
        [SerializeField] private RectTransform _rect;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _okArc;
        [SerializeField] private Image _goodArc;
        [SerializeField] private RectTransform _needle;
        [SerializeField] private Image[] _dots = System.Array.Empty<Image>();
        [SerializeField] private TextMeshProUGUI _grade;

        [SerializeField] private Color _good = new Color(0.45f, 0.85f, 0.40f);
        [SerializeField] private Color _okay = new Color(0.95f, 0.72f, 0.25f);
        [SerializeField] private Color _miss = new Color(0.95f, 0.40f, 0.35f);
        [SerializeField] private Color _dotEmpty = new Color(1f, 1f, 1f, 0.25f);
        [Tooltip("Seconds the last grade stays visible.")]
        [SerializeField, Min(0f)] private float _gradeDuration = 0.5f;

        public RectTransform Rect => _rect;

        public void Show(ChopDial dial, int cuts, int cutsNeeded, double now, float secondsSinceChop)
        {
            gameObject.SetActive(true);
            _group.alpha = 1f;

            // Arcs: radial fills start at the top and go clockwise, so rotate each to start at
            // (centre - half width). UI rotations are anticlockwise, hence the minus.
            SetArc(_okArc, dial.SweetSpot, dial.OkHalfWidth);
            SetArc(_goodArc, dial.SweetSpot, dial.GoodHalfWidth);
            _needle.localEulerAngles = new Vector3(0f, 0f, -dial.NeedleAngle(now));

            for (var i = 0; i < _dots.Length; i++)
            {
                _dots[i].gameObject.SetActive(i < cutsNeeded);
                _dots[i].color = i < cuts ? Color.white : _dotEmpty;
            }

            var grade = dial.LastGrade;
            var showGrade = grade.HasValue && cuts > 0 && secondsSinceChop < _gradeDuration;
            _grade.text = showGrade ? Label(grade.Value) : cuts == 0 ? "press X in the green" : string.Empty;
            _grade.color = showGrade ? Colour(grade.Value) : Color.white;
            _grade.alpha = showGrade ? 1f - secondsSinceChop / _gradeDuration * 0.6f : 0.8f;
        }

        public void Hide() => gameObject.SetActive(false);

        private static void SetArc(Image arc, float centre, float halfWidth)
        {
            arc.fillAmount = Mathf.Clamp01(halfWidth * 2f / 360f);
            arc.rectTransform.localEulerAngles = new Vector3(0f, 0f, -(centre - halfWidth));
        }

        private static string Label(ChopGrade grade) => grade switch
        {
            ChopGrade.Good => "Good!",
            ChopGrade.Early => "Early",
            ChopGrade.Late => "Late",
            _ => "Miss",
        };

        private Color Colour(ChopGrade grade) => grade == ChopGrade.Good ? _good : grade == ChopGrade.Miss ? _miss : _okay;
    }
}
