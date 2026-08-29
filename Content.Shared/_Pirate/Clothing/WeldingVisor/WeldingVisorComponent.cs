// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Clothing.WeldingVisor;

/// <summary>Pirate: welding visor - toggles eye protection with its raised state; ported from tgstation.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(WeldingVisorSystem))]
public sealed partial class WeldingVisorComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Lowered = true;

    [DataField, AutoNetworkedField]
    public string RaisedPrefix = "up";

    [DataField, AutoNetworkedField]
    public string? LoweredIconState = "icon";

    [DataField, AutoNetworkedField]
    public string? RaisedIconState = "icon-up";

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundLower;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundRaise;

    [DataField, AutoNetworkedField]
    public EntProtoId ToggleAction = "ActionToggleWeldingVisor";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;
}
