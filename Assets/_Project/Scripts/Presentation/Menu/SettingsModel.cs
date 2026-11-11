using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Gameplay.Display;
using Game.Gameplay.Saving;
using Game.Presentation.Hud;
using Game.Simulation.Realism;
using UnityEngine;

namespace Game.Presentation.Menu
{
    /// <summary>
    /// The settings rows, shared by the pause menu and the main menu (decision 2026-11-10): the
    /// realism preset (Relaxed / Standard / Chef, or Custom), the language, full screen or a window
    /// (decision 2026-11-11), and the five dials under
    /// Advanced (game design §12). Each change is saved at once to <c>settings.json</c> (shared by
    /// every slot) and applied to <see cref="RealismSettings"/>, which the simulation reads. Each menu
    /// draws the rows its own way.
    /// </summary>
    public sealed class SettingsModel
    {
        public readonly struct Row
        {
            public Row(string name, string value, string what, bool opens = false)
            {
                Name = name;
                Value = value;
                What = what;
                Opens = opens;
            }

            public string Name { get; }
            public string Value { get; }
            public string What { get; }

            /// <summary>A row that opens a page with A (Advanced) rather than changing sideways.</summary>
            public bool Opens { get; }
        }

        private enum Line
        {
            Preset,
            Language,
            Display,
            Advanced,
            Precision,
            Spoilage,
            Patience,
            Complexity,
            Hygiene,
        }

        private static readonly Line[] TopLines = { Line.Preset, Line.Language, Line.Display, Line.Advanced };
        private static readonly Line[] AdvancedLines = { Line.Precision, Line.Spoilage, Line.Patience, Line.Complexity, Line.Hygiene };

        private readonly RealismSettings _realism;
        private readonly HudStyle _style;

        public SettingsModel(RealismSettings realism, HudStyle style)
        {
            _realism = realism;
            _style = style;
            Reload();
        }

        public PlayerSettings Settings { get; private set; }

        /// <summary>True on the Advanced page (the five dials).</summary>
        public bool Advanced { get; private set; }

        /// <summary>The selected row on the current page.</summary>
        public int Selected { get; private set; }

        /// <summary>The preset the dials are now, or Custom.</summary>
        public RealismPreset Preset => _realism.Presets.Identify(Settings.Dials);

        /// <summary>Raised after the language changes, so a menu can redraw everything in it.</summary>
        public event System.Action LanguageChanged;

        private Line[] Lines => Advanced ? AdvancedLines : TopLines;

        /// <summary>Reads the settings file again and goes back to the first row of the top page.</summary>
        public void Reload()
        {
            Settings = PlayerSettings.Load(_realism);
            Advanced = false;
            Selected = 0;
        }

        public IReadOnlyList<Row> Rows => Lines.Select(RowFor).ToList();

        /// <summary>The page's title.</summary>
        public string Title => Advanced ? Tr.Get("Advanced") : Tr.Get("Settings");

        /// <summary>
        /// One frame of input: <paramref name="step"/> from the stick (y down the list, x to change),
        /// A and B. Returns false once B backs out of the top page, so the menu closes the settings.
        /// </summary>
        public bool Handle(Vector2Int step, bool south, bool east)
        {
            if (east)
            {
                if (!Advanced)
                    return false;
                Advanced = false;
                Selected = System.Array.IndexOf(TopLines, Line.Advanced);
                return true;
            }
            if (step.y != 0)
                Selected = Mathf.Clamp(Selected + step.y, 0, Lines.Length - 1);

            var line = Lines[Selected];
            if (south && line == Line.Advanced)
            {
                Advanced = true;
                Selected = 0;
                return true;
            }
            if (step.x != 0)
                Change(line, step.x);
            return true;
        }

        private void Change(Line line, int step)
        {
            if (line == Line.Display)
            {
                ChangeDisplay();
                return;
            }
            var dials = Settings.Dials;
            switch (line)
            {
                case Line.Preset:
                    dials = _realism.Presets.Get(RealismPresets.Step(Preset, step));
                    break;
                case Line.Language:
                    var choices = _style != null && _style.Languages != null ? _style.Languages.Choices().Select(c => c.Code).ToList() : new List<string> { "en" };
                    var index = Mathf.Max(0, choices.IndexOf(Settings.Language));
                    Settings.Language = choices[(index + step + choices.Count) % choices.Count];
                    break;
                case Line.Precision:
                    dials.CookingPrecision = Stepped(dials.CookingPrecision, step, 0.1f, 0f, 1f);
                    break;
                case Line.Spoilage:
                    dials.Spoilage = Stepped(dials.Spoilage, step, 0.25f, 0f, 3f);
                    break;
                case Line.Patience:
                    dials.Patience = Stepped(dials.Patience, step, 0.25f, 0.25f, 3f);
                    break;
                case Line.Complexity:
                    dials.MinigameComplexity = Stepped(dials.MinigameComplexity, step, 0.05f, 0f, 1f);
                    break;
                case Line.Hygiene:
                    dials.Hygiene = Stepped(dials.Hygiene, step, 0.25f, 0f, 3f);
                    break;
                default:
                    return;
            }
            Settings.Dials = dials;
            // Ctrl+Cmd+F may have switched the window while this page was open.
            if (!Application.isEditor)
                Settings.FullScreen = WindowMode.IsFullScreen;
            Settings.Save();
            if (_style != null && _style.Languages != null)
                _style.Languages.Load();
            Settings.ApplyTo(_realism);
            if (line == Line.Language)
                LanguageChanged?.Invoke();
        }

        // Either way sideways flips it. In the Editor it opens or closes the full-screen Game view and
        // isn't saved, so playing in the Editor never changes how the built game starts.
        private void ChangeDisplay()
        {
            var fullScreen = !WindowMode.IsFullScreen;
            WindowMode.Set(fullScreen);
            if (Application.isEditor)
                return;
            Settings.FullScreen = fullScreen;
            Settings.Save();
        }

        // On a grid of `size`, so the presets' values (0.25, 0.8…) are landed on exactly.
        private static float Stepped(float value, int step, float size, float min, float max) =>
            Mathf.Clamp(Mathf.Round(value / size + step) * size, min, max);

        private Row RowFor(Line line)
        {
            var dials = Settings.Dials;
            return line switch
            {
                Line.Preset => new Row(Tr.Get("Difficulty"), PresetName(Preset), PresetWhat(Preset)),
                Line.Language => new Row(Tr.Get("Language"), LanguageName(Settings.Language), Tr.Get("Everything, old reviews included, switches at once")),
                Line.Display => new Row(Tr.Get("Display"), WindowMode.IsFullScreen ? Tr.Get("Full screen") : Tr.Get("Windowed"), Tr.Get("The whole screen, or a window beside your other apps")),
                Line.Advanced => new Row(Tr.Get("Advanced"), "", Tr.Get("Each realism dial on its own"), opens: true),
                Line.Precision => new Row(Tr.Get("Cooking precision"), $"{dials.CookingPrecision * 100f:0}%", Tr.Get("How narrow the doneness windows are")),
                Line.Spoilage => new Row(Tr.Get("Spoilage"), $"×{dials.Spoilage:0.##}", Tr.Get("How fast food loses freshness")),
                Line.Patience => new Row(Tr.Get("Customer patience"), $"×{dials.Patience:0.##}", Tr.Get("How long guests wait")),
                Line.Complexity => new Row(Tr.Get("Minigame complexity"), $"{dials.MinigameComplexity * 100f:0}%", Tr.Get("How hard washing, cleaning and fires are")),
                _ => new Row(Tr.Get("Hygiene"), $"×{dials.Hygiene:0.##}", Tr.Get("How much mess a service leaves")),
            };
        }

        public static string PresetName(RealismPreset preset) => preset switch
        {
            RealismPreset.Relaxed => Tr.Get("Relaxed"),
            RealismPreset.Standard => Tr.Get("Standard"),
            RealismPreset.Chef => Tr.In("difficulty", "Chef"),
            _ => Tr.Get("Custom"),
        };

        private static string PresetWhat(RealismPreset preset) => preset switch
        {
            RealismPreset.Relaxed => Tr.Get("Wide windows, patient guests, little mess"),
            RealismPreset.Standard => Tr.Get("The kitchen as it's meant to be played"),
            RealismPreset.Chef => Tr.Get("Tight windows, impatient guests, more mess"),
            _ => Tr.Get("Your own mix, set under Advanced"),
        };

        private string LanguageName(string code) =>
            _style != null && _style.Languages != null ? _style.Languages.Choices().FirstOrDefault(c => c.Code == code).Name ?? "English" : "English";
    }
}
