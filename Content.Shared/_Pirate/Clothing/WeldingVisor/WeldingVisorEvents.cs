// SPDX-License-Identifier: MIT

using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Clothing.WeldingVisor;

/// <summary>Pirate: welding visor - toggles the visor.</summary>
public sealed partial class ToggleWeldingVisorEvent : InstantActionEvent;

/// <summary>Pirate: welding visor - raised after toggling.</summary>
[ByRefEvent]
public readonly record struct WeldingVisorToggledEvent(EntityUid? Wearer, bool Lowered);

/// <summary>Pirate: welding visor - sprite state key.</summary>
[Serializable, NetSerializable]
public enum WeldingVisorVisuals : byte
{
    Lowered,
}
