using Content.Shared.Actions;

namespace Content.Shared._Pirate.BountyHunter;

/// <summary>
/// Raised when using the WeaponRecall action.
/// </summary>
[ByRefEvent]
public sealed partial class OnWeaponRecallActionEvent : InstantActionEvent;
