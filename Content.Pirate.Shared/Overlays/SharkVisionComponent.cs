// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Overlays;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Overlays;

[RegisterComponent, NetworkedComponent]
public sealed partial class SharkVisionComponent : SwitchableVisionOverlayComponent
{
    [DataField]
    public override EntProtoId? ToggleAction { get; set; } = "ActionSharkVisionPulse";

    [DataField]
    public override Color Color { get; set; } = Color.FromHex("#fc0800ff");

    /// <summary>
    /// Reagents that count as blood for the purposes of this overlay.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype>[] BloodPrototypes =
    [
        "Blood",
        "InsectBlood",
        "AmmoniaBlood",
        "CopperBlood",
        "ZombieBlood",
        "AvaliBlood",
        "BlackBlood",
        "AlienBlood",
        "BloodChangeling",
    ];
}
