using System;
using System.Collections.Generic;
using Game.Simulation.Campaign;

namespace Game.Gameplay.Debt
{
    /// <summary>
    /// The personal mobile (§15.6): the boss rings it when a collection is near, missed or paid.
    /// Gameplay rings; the phone in the bottom right corner (BossPhoneHud) shows the call.
    /// </summary>
    public sealed class BossPhone
    {
        /// <summary>Raised with why he's calling and what he says.</summary>
        public event Action<BossCallKind, IReadOnlyList<string>> Rang;

        /// <summary>The latest call, for anything that starts after it rang.</summary>
        public BossCallKind Last { get; private set; }

        public void Ring(BossCallKind kind, IReadOnlyList<string> lines)
        {
            if (kind == BossCallKind.None || lines == null || lines.Count == 0)
                return;
            Last = kind;
            Rang?.Invoke(kind, lines);
        }
    }
}
