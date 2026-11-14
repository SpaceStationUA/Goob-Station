using Content.Shared.Actions;

namespace Content.Shared._DV.Psionics.Events.PowerActionEvents;

/// <summary>
/// This gets fired when someone uses the Xenoglossy action.
/// Xenoglossy is a passive power — the action is informational only.
/// </summary>
public sealed partial class XenoglossyPowerActionEvent : InstantActionEvent;
