using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.PAI;

public sealed partial class PAIToggleNightVisionEvent : InstantActionEvent;

public sealed partial class PAIToggleThermalVisionEvent : InstantActionEvent;

public sealed partial class PAILightFlickerEvent : InstantActionEvent;

public sealed partial class PAIHealthScanEvent : InstantActionEvent;

public sealed partial class PAIToggleFlashlightEvent : InstantActionEvent;

public sealed partial class PAIToggleMedHudEvent : InstantActionEvent;
