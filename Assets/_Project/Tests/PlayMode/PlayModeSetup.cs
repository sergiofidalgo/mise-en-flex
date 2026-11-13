using Game.Gameplay;
using NUnit.Framework;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Once for the whole PlayMode suite: players join without naming themselves (2026-11-13), as
    /// most tests join a chef and drive them at once. The naming tests switch it on for themselves.
    /// </summary>
    [SetUpFixture]
    public sealed class PlayModeSetup
    {
        [OneTimeSetUp]
        public void JoinWithoutNaming() => JoinNaming.Enabled = false;

        [OneTimeTearDown]
        public void Restore() => JoinNaming.Enabled = true;
    }
}
