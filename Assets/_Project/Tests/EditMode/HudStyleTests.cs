using Game.Gameplay.Stations;
using Game.Presentation.Hud;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class HudStyleTests
    {
        // Gamepad shows the Xbox letters; the keyboard mirrors them as X/D/A/W with Q/E for triggers.
        [TestCase(PromptControl.South, "A", "X")]
        [TestCase(PromptControl.East, "B", "D")]
        [TestCase(PromptControl.West, "X", "A")]
        [TestCase(PromptControl.North, "Y", "W")]
        [TestCase(PromptControl.Triggers, "LT RT", "Q E")]
        public void BadgeGlyphsMatchTheDevice(PromptControl control, string gamepad, string keyboard)
        {
            Assert.That(HudStyle.Glyph(control, gamepad: true), Is.EqualTo(gamepad));
            Assert.That(HudStyle.Glyph(control, gamepad: false), Is.EqualTo(keyboard));
        }
    }
}
