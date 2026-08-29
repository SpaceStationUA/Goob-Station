// SPDX-License-Identifier: MIT

using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Clothing.WeldingVisor;

/// <summary>
/// Pirate: welding visor - action event raised to toggle a welding mask/goggle's visor between
/// lowered (protecting) and raised (not protecting).
/// </summary>
public sealed partial class ToggleWeldingVisorEvent : InstantActionEvent;

/// <summary>
/// Pirate: welding visor - raised on the item when its visor is toggled, so other systems (e.g. hiding
/// snout/ear layers only while actually covering the face) can react live, not just on equip.
/// </summary>
[ByRefEvent]
public readonly record struct WeldingVisorToggledEvent(EntityUid? Wearer, bool Lowered);

/// <summary>
/// Pirate: welding visor - appearance data key used to drive the item's own (dropped/inventory) sprite state.
/// </summary>
[Serializable, NetSerializable]
public enum WeldingVisorVisuals : byte
{
    Lowered,
}
