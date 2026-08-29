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
    /// <summary>
    /// Whether the visor is currently lowered over the eyes. Only while lowered does it protect against
    /// welding flashes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Lowered = true;

    /// <summary>
    /// Sprite/clothing equipped-prefix applied while the visor is raised (not protecting).
    /// The matching "&lt;prefix&gt;-equipped-SLOT"/"&lt;prefix&gt;-inhand-*" states must exist on the item's RSI.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string RaisedPrefix = "up";

    /// <summary>
    /// RSI state used for the item's own (dropped/inventory) sprite - and its action's icon - while lowered.
    /// Set to null to leave the item's base sprite/action icon untouched when toggling.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? LoweredIconState = "icon";

    /// <summary>
    /// RSI state used for the item's own (dropped/inventory) sprite - and its action's icon - while raised.
    /// See <see cref="LoweredIconState"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? RaisedIconState = "icon-up";

    /// <summary>
    /// Played when the visor is lowered. Null (the default) plays nothing - matches tgstation, where only the
    /// welding gas mask plays a sound on toggle; the welding masks/goggles are silent.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundLower;

    /// <summary>
    /// Played when the visor is raised. See <see cref="SoundLower"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundRaise;

    [DataField, AutoNetworkedField]
    public EntProtoId ToggleAction = "ActionToggleWeldingVisor";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;
}
