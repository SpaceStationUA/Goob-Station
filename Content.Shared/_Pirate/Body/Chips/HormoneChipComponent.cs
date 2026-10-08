// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Body.Chips;

[RegisterComponent, NetworkedComponent]
public sealed partial class HormoneChipComponent : Component
{
    [DataField]
    public int CritThresholdModifier;

    [DataField]
    public float StamcritMultiplier = 1f;

    [DataField]
    public float HeatThresholdMultiplier = 1f;

    [DataField]
    public float ColdThresholdMultiplier = 1f;

    [DataField]
    public bool LightStep;
}
