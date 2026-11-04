using System;
using Game.Simulation.Items;

namespace Game.Simulation.Staff
{
    /// <summary>
    /// What a staff member is doing, as gameplay needs to know it: where to walk them and what they
    /// carry. The job decides; gameplay moves the body and reports arrival.
    /// </summary>
    public interface IStaffJob
    {
        StaffMember Member { get; }

        /// <summary>The simulation object they're walking to (a sink, a counter's slot...), or null.</summary>
        object Target { get; }

        /// <summary>What's in their hands, or null.</summary>
        Item Carried { get; }

        void Tick(float seconds);

        /// <summary>Gameplay: they've reached <see cref="Target"/>.</summary>
        void Arrived();

        /// <summary>They've been let go: what they're carrying goes back where it belongs.</summary>
        void Release();

        /// <summary>Raised when an item leaves sight (stored, dropped), so its view can go.</summary>
        event Action<Item> ItemGone;

        /// <summary>Raised when food changed form (cut), so views can catch up.</summary>
        event Action FoodChanged;
    }
}
