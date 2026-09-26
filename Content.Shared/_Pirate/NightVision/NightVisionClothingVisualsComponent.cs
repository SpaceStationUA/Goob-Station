// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.NightVision;

/// <summary>Controls the worn sprite prefix and visualizer state for clothing with built-in night vision.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NightVisionClothingVisualsComponent : Component
{
    [DataField]
    public string EnabledPrefix = "on";
}

[Serializable, NetSerializable]
public enum NightVisionClothingVisuals : byte
{
    Enabled,
}

[ByRefEvent]
public readonly record struct MonolithNightVisionToggledEvent(bool Enabled);
