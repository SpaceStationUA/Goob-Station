// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Medical.LimbFixation;

/// <summary>
/// Prevents traumatic dismemberment and converts it into a surgically repairable loss of function.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LimbFixationComponent : Component;

/// <summary>
/// Marks a body part whose connection survived critical damage but no longer functions.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LimbFixationDamageComponent : Component;

/// <summary>
/// Tracks body parts disabled by <see cref="LimbFixationSystem"/>, so other disable sources are preserved.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(LimbFixationSystem))]
public sealed partial class LimbFixationDisabledComponent : Component;

/// <summary>
/// Restores a body part disabled by limb fixation damage when its surgery step is completed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryRestoreLimbFunctionStepComponent : Component;

/// <summary>
/// Prevents a surgery from being performed before limb fixation damage is restored.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryFunctionalPartConditionComponent : Component;

/// <summary>
/// Raised before a damaging or otherwise traumatic amputation. Surgical removal uses the safe amputation path.
/// </summary>
[ByRefEvent]
public record struct BeforeTraumaticAmputationEvent(EntityUid Part, bool Cancelled = false);
