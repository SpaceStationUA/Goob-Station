using Content.Shared.Actions;

namespace Content.Shared._DV.Psionics.Events.PowerActionEvents;

/// <summary>
/// This gets fired when someone uses the MetapsionicPulse action.
/// The pulse is instant - it scans around the caster, so no world targeting is needed.
/// </summary>
public sealed partial class MetapsionicPulsePowerActionEvent : InstantActionEvent;
