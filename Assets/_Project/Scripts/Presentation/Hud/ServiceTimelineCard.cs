using Game.Data;
using System.Collections.Generic;
using Game.Simulation.Service;
using Game.Simulation.Street;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The day card in service (the user's pick, 2026-11-14, "B2"): a name plate like the money
    /// card's, the time big on the left and "Day 1 · Dinner" after it, over a timeline of the whole
    /// service. The timeline is coloured by the sky of the hours it runs through, marks the rushes
    /// with flames and last orders with red stripes, dims what's gone, and carries a sun or moon at
    /// the clock's hour. The gold band along the bottom drains as the service runs. In last orders
    /// the time, the band and the marker's glow go red and pulse; as a rush starts a callout pops
    /// up above the card. Built in code, outside Play mode too, for stills.
    /// </summary>
    internal sealed class ServiceTimelineCard
    {
        public const float Width = 470f;
        public const float Height = 136f;
        private const float Pad = 24f;
        private const float TimeSize = 40f, LabelSize = 24f, HeaderBaseline = 50f, LabelGap = 14f;
        private const float TrackTop = 62f, TrackHeight = 26f, TrackRadius = 9f, TrackInk = 3f;
        private const float TickGap = 4f, TickLong = 9f, TickShort = 5f, TickWidth = 2.5f, HourSize = 17f;
        private const float Marker = 26f, MarkerInk = 3f, Glow = 14f;
        private const float StripePitch = 12f;
        private const float FlameSize = 17f;
        private const float CalloutHeight = 40f, CalloutSize = 24f, CalloutPad = 16f, CalloutLift = 12f;
        private static readonly Color Tick = new Color(1f, 1f, 1f, 0.4f);
        private static readonly Color Past = new Color(0f, 0f, 0f, 0.7f); // blended in linear light: about half as bright to the eye
        private static readonly Color FlameOuter = new Color(1f, 0.45f, 0.1f);
        private static readonly Color FlameInner = new Color(1f, 0.85f, 0.3f);

        private readonly HudStyle _style;
        private readonly RectTransform _root;
        private readonly CanvasGroup _group;
        private string _built;
        private Texture2D _sky;
        private float _timeWidth;
        private TextMeshProUGUI _time;
        private TextMeshProUGUI _label;
        private string _labelShown;
        private Image _band;
        private RectTransform _spent;
        private RectTransform _spentBand;
        private RectTransform _past;
        private RawImage _stripes;
        private RectTransform _marker;
        private Image _glow;
        private Image _sun;
        private Image _moon;
        private Image _bite;
        private RectTransform _callout;
        private CanvasGroup _calloutGroup;
        private TextMeshProUGUI _calloutText;
        private float _calloutAt = float.NegativeInfinity;

        public ServiceTimelineCard(HudStyle style, Transform canvas, float margin)
        {
            _style = style;
            _root = UiKit.Rect(canvas, "ServiceTimeline", 0f, 0f, Width, Height);
            _root.anchorMin = _root.anchorMax = _root.pivot = Vector2.zero;
            _root.anchoredPosition = new Vector2(margin, margin);
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        public float Alpha
        {
            get => _group.alpha;
            set => _group.alpha = value;
        }

        /// <summary>
        /// Draws the card for <paramref name="elapsed"/> in-game seconds into the service;
        /// <paramref name="now"/> (seconds, any steady clock) drives the pulses and the callout.
        /// </summary>
        public void Draw(int day, string slot, ServiceSettings settings, ServicePhase phase, float elapsed, float now)
        {
            var opens = ServiceTimeline.OpensAt(settings);
            var hours = ServiceTimeline.Hours(settings);
            var lastOrders = ServiceTimeline.LastOrdersFrom(settings);
            var key = $"{opens}|{hours}|{lastOrders}|{settings.RushHours?.Length}|{settings.RushHalfWidthHours}";
            if (key != _built)
            {
                _built = key;
                _labelShown = null;
                Build(settings);
            }

            var fraction = ServiceTimeline.Fraction(settings, elapsed);
            var hour = opens + hours * fraction;
            var closed = phase == ServicePhase.Closed || phase == ServicePhase.Finished;
            var urgent = phase == ServicePhase.LastOrders;
            var pulse = urgent ? 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * _style.UrgentPulse * now) : 0f;

            _time.text = DayCardText.Clock(DayCardText.Minutes(opens, elapsed, settings.Clock?.SecondsPerHour ?? 3600f));
            _time.color = urgent ? _style.ServiceUrgent : _style.TextColor;
            Label(day, slot, urgent, closed);

            // What's gone dims; the band keeps what's left.
            var trackWidth = Width - 2f * Pad;
            _past.localScale = new Vector3(fraction, 1f, 1f); // a full-width box squeezed from the left: a zero-wide one never drew
            Drain(1f - fraction, urgent ? Color.Lerp(_style.ServiceUrgent, Color.white, 0.25f * pulse) : _style.MoneyEdge);
            _stripes.color = WithAlpha(_style.ServiceUrgent, urgent ? 0.75f + 0.25f * pulse : 0.75f);

            // The marker: a sun by day, a moon with a bite of the sky by night, red-glowing in last orders.
            _marker.anchoredPosition = new Vector2(Pad + trackWidth * fraction, -(TrackTop + TrackHeight / 2f));
            var night = Daylight.Nightfall(hour) > 0.5f;
            _sun.enabled = !night;
            _moon.enabled = _bite.enabled = night;
            _bite.color = _style.ServiceSky(hour);
            _glow.enabled = urgent;
            _glow.color = WithAlpha(_style.ServiceUrgent, 0.35f + 0.3f * pulse);
            var glow = Marker + 2f * MarkerInk + Glow * (0.8f + 0.4f * pulse);
            _glow.rectTransform.sizeDelta = new Vector2(glow, glow);
            _glow.rectTransform.anchoredPosition = new Vector2(-glow / 2f, glow / 2f);

            Callout(now);
        }

        /// <summary>Takes the rush callout down at once.</summary>
        public void ClearCallout() => _calloutAt = float.NegativeInfinity;

        /// <summary>Pops the rush callout ("Dinner rush!") up above the card.</summary>
        public void Pop(string text, float now)
        {
            _calloutText.text = text;
            var width = UiKit.Width(_calloutText) + 2f * CalloutPad;
            UiKit.Clear(_callout);
            UiKit.StrokedBox(_callout, "Pill", _style.RushCallout, _style.PlateInk, _style.PlateStroke * 0.6f, 0f, 0f, width, CalloutHeight, CalloutHeight / 2f);
            _calloutText = UiKit.TextAt(_style.DisplayFont, _callout, "Text", text, CalloutSize, _style.PlateInk, width / 2f, CalloutHeight / 2f + CalloutSize * 0.36f, width, HorizontalAlignmentOptions.Center);
            _callout.sizeDelta = new Vector2(width, CalloutHeight);
            _calloutAt = now;
        }

        private void Label(int day, string slot, bool urgent, bool closed)
        {
            var title = DayCardText.Title(day);
            var what = urgent ? Tr.Get("Last orders!") : closed ? Tr.Get("Closed") : slot;
            var colour = urgent ? _style.ServiceUrgent : closed ? _style.DayNotice : _style.TextColor;
            var text = string.IsNullOrEmpty(what) ? title : $"{title} · <color=#{ColorUtility.ToHtmlStringRGB(colour)}>{what}</color>";
            if (text == _labelShown)
                return;
            _labelShown = text;
            _label.text = text;
        }

        // The band shows what's left in gold; the rest of it, from the right, is covered in the dark.
        private void Drain(float left, Color colour)
        {
            _band.color = colour;
            var half = _style.PlateStroke / 2f;
            var fillWidth = Width - _style.PlateStroke;
            var from = half + fillWidth * Mathf.Clamp01(left);
            _spent.anchoredPosition = new Vector2(from, _spent.anchoredPosition.y);
            _spent.sizeDelta = new Vector2(Mathf.Max(0f, half + fillWidth - from), _spent.sizeDelta.y);
            _spentBand.anchoredPosition = new Vector2(half - from, _spentBand.anchoredPosition.y);
        }

        private void Callout(float now)
        {
            var t = now - _calloutAt;
            var length = _style.RushCalloutSeconds;
            if (t < 0f || t > length)
            {
                _calloutGroup.alpha = 0f;
                return;
            }
            const float grow = 0.18f, settle = 0.12f, fade = 0.5f;
            // Pops past full size and settles back, holds, then fades as it lifts a little.
            var scale = t < grow ? Mathf.Lerp(0.6f, 1.1f, t / grow) : t < grow + settle ? Mathf.Lerp(1.1f, 1f, (t - grow) / settle) : 1f;
            var out01 = Mathf.Clamp01((t - (length - fade)) / fade);
            _calloutGroup.alpha = Mathf.Min(Mathf.Clamp01(t / grow), 1f - out01);
            _callout.localScale = new Vector3(scale, scale, 1f);
            _callout.anchoredPosition = new Vector2(0f, CalloutLift + 6f * out01);
        }

        private void Build(ServiceSettings settings)
        {
            UiKit.Clear(_root);
            if (_sky != null)
                Object.DestroyImmediate(_sky);

            var stroke = _style.PlateStroke;
            NamePlate.Draw(_style, _root, _style.MoneyEdge, 0f, 0f, Width, Height);
            _band = _root.Find("Band").GetComponent<Image>();
            SpentBand(stroke);

            _time = UiKit.TextAt(_style.DisplayFont, _root, "Time", "88:88", TimeSize, _style.TextColor, Pad, HeaderBaseline, 200f);
            _timeWidth = UiKit.Width(_time); // the widest digits, so the label never shifts as minutes tick
            var labelX = Pad + _timeWidth + LabelGap;
            _label = UiKit.TextAt(_style.Bold, _root, "Label", "", LabelSize, _style.DayText, labelX, HeaderBaseline, Width - labelX - Pad);

            var trackWidth = Width - 2f * Pad;
            UiKit.RoundBox(_root, "TrackInk", _style.PlateInk, Pad - TrackInk, TrackTop - TrackInk, trackWidth + 2f * TrackInk, TrackHeight + 2f * TrackInk, TrackRadius + TrackInk);
            var track = UiKit.RoundBox(_root, "Track", Color.white, Pad, TrackTop, trackWidth, TrackHeight, TrackRadius);
            track.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            _sky = SkyTexture(ServiceTimeline.OpensAt(settings), ServiceTimeline.Hours(settings));
            var sky = UiKit.Rect(track.transform, "Sky", 0f, 0f, trackWidth, TrackHeight).gameObject.AddComponent<RawImage>();
            sky.texture = _sky;
            sky.raycastTarget = false;

            foreach (var rush in ServiceTimeline.Rushes(settings))
            {
                var x = rush.From * trackWidth;
                var width = (rush.To - rush.From) * trackWidth;
                UiKit.RoundBox(track.transform, "Rush", _style.ServiceRush, x, 0f, width, TrackHeight, 5f);
                Flame(track.transform, x + width / 2f, TrackHeight / 2f + 1f);
            }

            var lastOrders = ServiceTimeline.LastOrdersFrom(settings);
            var stripesX = lastOrders * trackWidth;
            _stripes = UiKit.Rect(track.transform, "LastOrders", stripesX, 0f, trackWidth - stripesX, TrackHeight).gameObject.AddComponent<RawImage>();
            _stripes.texture = StripeTexture();
            _stripes.uvRect = new Rect(0f, 0f, (trackWidth - stripesX) / StripePitch, TrackHeight / StripePitch);
            _stripes.raycastTarget = false;

            _past = UiKit.RoundBox(track.transform, "Past", Past, 0f, 0f, trackWidth, TrackHeight, 0f).rectTransform;

            var tickTop = TrackTop + TrackHeight + TickGap;
            foreach (var mark in ServiceTimeline.HourMarks(settings))
            {
                var x = Pad + mark.Fraction * trackWidth;
                var length = mark.Labelled ? TickLong : TickShort;
                UiKit.RoundBox(_root, "Tick", Tick, x - TickWidth / 2f, tickTop, TickWidth, length, 1f);
                if (mark.Labelled)
                    UiKit.TextAt(_style.Bold, _root, "Hour", mark.Hour.ToString(), HourSize, _style.DayText, x, tickTop + TickLong + 4f + HourSize * 0.72f, 40f, HorizontalAlignmentOptions.Center);
            }

            BuildMarker();

            _callout = UiKit.Rect(_root, "RushCallout", 0f, 0f, 10f, CalloutHeight);
            _callout.anchorMin = _callout.anchorMax = _callout.pivot = new Vector2(0f, 1f); // grows from its bottom-left, on the card's top edge
            _callout.pivot = new Vector2(0f, 0f);
            _calloutGroup = _callout.gameObject.AddComponent<CanvasGroup>();
            _calloutGroup.alpha = 0f;
            _calloutText = UiKit.TextAt(_style.DisplayFont, _callout, "Text", "", CalloutSize, _style.PlateInk, 0f, 0f, 400f);
        }

        // A copy of the plate's band, darker, in a clip that starts where the gold ends.
        private void SpentBand(float stroke)
        {
            var half = stroke / 2f;
            var fillWidth = Width - stroke;
            var bottom = Height - half;
            var bandHeight = _style.PlateBand;
            var inner = Mathf.Max(0f, _style.PlateRadius - half);
            var rounded = Mathf.Max(bandHeight, inner) + bandHeight;
            _spent = UiKit.Rect(_root, "Spent", half + fillWidth, bottom - bandHeight, 0f, bandHeight);
            _spent.gameObject.AddComponent<RectMask2D>();
            var spent = Color.Lerp(_style.CardBackground, Color.black, 0.35f);
            spent.a = 1f;
            _spentBand = UiKit.RoundBox(_spent, "Band", spent, 0f, bandHeight - rounded, fillWidth, rounded, inner).rectTransform;
        }

        private void BuildMarker()
        {
            _marker = UiKit.Rect(_root, "Marker", 0f, 0f, 0f, 0f);
            _glow = UiKit.Ellipse(_marker, "Glow", _style.ServiceUrgent, 0f, 0f, Marker + 2f * MarkerInk + Glow, Marker + 2f * MarkerInk + Glow);
            var outer = Marker + 2f * MarkerInk;
            UiKit.Ellipse(_marker, "Ink", _style.PlateInk, -outer / 2f, -outer / 2f, outer, outer);
            _sun = UiKit.Ellipse(_marker, "Sun", _style.ServiceSun, -Marker / 2f, -Marker / 2f, Marker, Marker);
            _moon = UiKit.Ellipse(_marker, "Moon", _style.ServiceMoon, -Marker / 2f, -Marker / 2f, Marker, Marker);
            _moon.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var bite = Marker * 0.72f;
            _bite = UiKit.Ellipse(_moon.transform, "Bite", Color.black, Marker * 0.42f, -Marker * 0.12f, bite, bite);
        }

        // A little flame: a round base with a square turned 45° on top for the tip, and a yellow core.
        private void Flame(Transform parent, float cx, float cy)
        {
            void Drop(string name, Color colour, float size, float y)
            {
                UiKit.Ellipse(parent, name, colour, cx - size / 2f, y - size / 2f, size, size);
                var side = size * 0.72f;
                var tip = UiKit.RoundBox(parent, name + "Tip", colour, 0f, 0f, side, side, 1.5f).rectTransform;
                tip.pivot = new Vector2(0.5f, 0.5f);
                tip.anchoredPosition = new Vector2(cx, -(y - size * 0.22f));
                tip.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            Drop("Flame", FlameOuter, FlameSize, cy + FlameSize * 0.12f);
            Drop("Core", FlameInner, FlameSize * 0.5f, cy + FlameSize * 0.25f);
        }

        private Texture2D SkyTexture(float opens, float hours)
        {
            const int size = 128;
            var texture = new Texture2D(size, 1, TextureFormat.RGBA32, false) { name = "ServiceSky", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color[size];
            for (var i = 0; i < size; i++)
                pixels[i] = _style.ServiceSky(opens + hours * i / (size - 1f));
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D _stripeTexture;

        // White diagonal stripes on clear, tiled and tinted red for last orders.
        private static Texture2D StripeTexture()
        {
            if (_stripeTexture != null)
                return _stripeTexture;
            const int size = 16;
            _stripeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "LastOrdersStripes", wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = (x + y) % size < size / 2 ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
            _stripeTexture.SetPixels32(pixels);
            _stripeTexture.Apply();
            return _stripeTexture;
        }

        private static Color WithAlpha(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }
    }
}
