// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Clothing.WeldingVisor;

/// <summary>
/// Pirate: welding visor - lets welding masks/goggles be flipped between a lowered (eye-protecting) and
/// raised (non-protecting) state, mirroring tgstation's welding mask/goggle "up" behaviour.
/// Only <see cref="Lowered"/> welding visors count towards eye/flash protection - see WeldingVisorSystem
/// and the checks added to EyeProtectionSystem/SharedFlashSystem.
/// </summary>
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
