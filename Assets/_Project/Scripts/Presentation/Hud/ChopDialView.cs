using Game.Data;
using Game.Simulation.Prep;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The cutting dial: a half-arc fan with the ingredient's sweet spot on its rim (amber edges
    /// for Early/Late, a green core for Good) and a needle swinging far right to far left and back.
    /// Every cut is made obvious: a marker where you pressed, the fan flashes in the grade's colour
    /// (a pop for Good, a shake for a Miss), the grade and the speed change it caused, a dot per cut
    /// coloured by its grade, and a cut-quality bar that drops when you're penalised.
    /// </summary>
    public sealed class ChopDialView : MonoBehaviour
    {
        [SerializeField] private RectTransform _rect;
        [Tooltip("Everything that pops or shakes; moved relative to the dial's anchor.")]
        [SerializeField] private RectTransform _content;
        [SerializeField] private CanvasGroup _group;
        [Tooltip("The dark fan and inner fan that frame the arc; sized to the sweep.")]
        [SerializeField] private Image _backing;
        [SerializeField] private Image _inner;
        [SerializeField] private Image _okArc;
        [SerializeField] private Image _goodArc;
        [Tooltip("Fan-shaped overlay tinted with the last grade's colour for a moment after each cut.")]
        [SerializeField] private Image _flash;
        [SerializeField] private RectTransform _needle;
        [Tooltip("Rotates about the centre; its child tick marks where the last cut landed.")]
        [SerializeField] private RectTransform _markerArm;
        [SerializeField] private Image _marker;
        [SerializeField] private Image[] _dots = System.Array.Empty<Image>();
        [SerializeField] private Image _qualityFill;
        [SerializeField] private TextMeshProUGUI _grade;
        [SerializeField] private TextMeshProUGUI _speedHint;

        [Header("Colours")]
        [SerializeField] private Color _good = new Color(0.45f, 0.85f, 0.40f);
        [SerializeField] private Color _okay = new Color(0.95f, 0.72f, 0.25f);
        [SerializeField] private Color _miss = new Color(0.95f, 0.35f, 0.30f);
        [SerializeField] private Color _dotEmpty = new Color(1f, 1f, 1f, 0.22f);

        [Header("Feedback timing and strength")]
        [SerializeField, Min(0.05f)] private float _flashDuration = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _flashStrength = 0.65f;
        [SerializeField, Min(0.05f)] private float _markerDuration = 0.8f;
        [SerializeField, Min(0.05f)] private float _gradeDuration = 0.7f;
        [Tooltip("Extra scale at the start of the Good pop.")]
        [SerializeField, Min(0f)] private float _goodPop = 0.14f;
        [Tooltip("Canvas pixels of side-to-side shake on a Miss.")]
        [SerializeField, Min(0f)] private float _missShake = 10f;
        [SerializeField, Min(0f)] private float _shakeFrequency = 55f;
        [SerializeField, Min(8f)] private float _gradeSize = 30f;
        [SerializeField, Min(8f)] private float _missGradeSize = 38f;

        public RectTransform Rect => _rect;

        public void Show(ChopDial dial, int cuts, int cutsNeeded, double now, float secondsSinceChop)
        {
            gameObject.SetActive(true);
            _group.alpha = 1f;

            // The arc is centred on the top of the dial. A position along the sweep (0 = right end)
            // maps to a clockwise-from-top angle of (sweep / 2 - position).
            var half = dial.SweepDegrees / 2f;
            SetArc(_backing, 0f, half);
            SetArc(_inner, 0f, half);
            SetArc(_flash, 0f, half);
            SetArc(_okArc, half - dial.SweetSpot, dial.OkHalfWidth);
            SetArc(_goodArc, half - dial.SweetSpot, dial.GoodHalfWidth);
            _needle.localEulerAngles = new Vector3(0f, 0f, -(half - dial.NeedlePosition(now)));

            var grade = dial.LastGrade;
            var colour = grade.HasValue ? Colour(grade.Value) : Color.white;
            var fresh = grade.HasValue && cuts > 0;

            // A dot per cut, coloured by how that cut went.
            for (var i = 0; i < _dots.Length; i++)
            {
                _dots[i].gameObject.SetActive(i < cutsNeeded);
                _dots[i].color = i < dial.Grades.Count ? Colour(dial.Grades[i]) : _dotEmpty;
            }

            // Cut quality so far: the evenness that feeds the dish score.
            _qualityFill.fillAmount = dial.Evenness;
            _qualityFill.color = dial.Evenness >= 0.8f ? _good : dial.Evenness >= 0.5f ? _okay : _miss;

            // Where the last cut landed.
            var markerT = fresh ? secondsSinceChop / _markerDuration : 1f;
            _marker.enabled = markerT < 1f && dial.LastHitPosition.HasValue;
            if (_marker.enabled)
            {
                _markerArm.localEulerAngles = new Vector3(0f, 0f, -(half - dial.LastHitPosition.Value));
                _marker.color = new Color(colour.r, colour.g, colour.b, 1f - markerT * markerT);
            }

            // The fan flashes in the grade's colour.
            var flashT = fresh ? secondsSinceChop / _flashDuration : 1f;
            _flash.enabled = flashT < 1f;
            _flash.color = new Color(colour.r, colour.g, colour.b, _flashStrength * (1f - flashT));

            // A pop for Good, a shake for a Miss, a small nudge otherwise.
            var punchT = Mathf.Clamp01(flashT);
            _content.localScale = Vector3.one;
            _content.anchoredPosition = Vector2.zero;
            if (fresh && punchT < 1f)
            {
                if (grade == ChopGrade.Good)
                    _content.localScale = Vector3.one * (1f + _goodPop * (1f - punchT));
                else if (grade == ChopGrade.Miss)
                    _content.anchoredPosition = new Vector2(Mathf.Sin(secondsSinceChop * _shakeFrequency) * _missShake * (1f - punchT), 0f);
                else
                    _content.localScale = Vector3.one * (1f + _goodPop * 0.3f * (1f - punchT));
            }

            // The grade, and the speed change it caused.
            var showGrade = fresh && secondsSinceChop < _gradeDuration;
            _grade.text = showGrade ? Label(grade.Value) : cuts == 0 ? Tr.Get("press X in the green") : string.Empty;
            _grade.color = showGrade ? colour : new Color(1f, 1f, 1f, 0.8f);
            _grade.fontSize = showGrade && grade == ChopGrade.Miss ? _missGradeSize : _gradeSize;
            _speedHint.text = showGrade ? (grade == ChopGrade.Good ? Tr.Get("faster") : Tr.Get("slower")) : string.Empty;
            _speedHint.alpha = showGrade ? 1f - secondsSinceChop / _gradeDuration : 0f;
        }

        public void Hide() => gameObject.SetActive(false);

        // Radial fills start at the top and go clockwise, so rotate each to start at
        // (centre - half width). UI rotations are anticlockwise, hence the minus.
        private static void SetArc(Image arc, float centre, float halfWidth)
        {
            arc.fillAmount = Mathf.Clamp01(halfWidth * 2f / 360f);
            arc.rectTransform.localEulerAngles = new Vector3(0f, 0f, -(centre - halfWidth));
        }

        private static string Label(ChopGrade grade) => grade switch
        {
            ChopGrade.Good => Tr.Get("Good!"),
            ChopGrade.Early => Tr.Get("Early"),
            ChopGrade.Late => Tr.Get("Late"),
            _ => Tr.Get("Miss!"),
        };

        private Color Colour(ChopGrade grade) => grade == ChopGrade.Good ? _good : grade == ChopGrade.Miss ? _miss : _okay;
    }
}
