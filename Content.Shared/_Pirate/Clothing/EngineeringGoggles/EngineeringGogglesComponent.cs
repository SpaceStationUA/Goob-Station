// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Clothing.EngineeringGoggles;

/// <summary>
/// Pirate: engineering goggles - ported from tgstation's "engineering scanner goggles"
/// (/obj/item/clothing/glasses/meson/engine). Cycles a single toggle action between three modes, driving the
/// existing <see cref="Content.Shared._Pirate.Xray.XRayVisionComponent"/> (tgstation's "meson" mode - see
/// basic structure/terrain through walls) and <see cref="Content.Shared.SubFloor.TrayScannerComponent"/>
/// (tgstation's "t-ray" mode - see subfloor pipes/cables) mutually exclusively, both of which must also be
/// present on the entity. Neither of those components should carry their own toggle action/sound - this
/// component's action is the only one offered, see EngineeringGogglesSystem.SetMode.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(EngineeringGogglesSystem))]
public sealed partial class EngineeringGogglesComponent : Component
{
    [DataField, AutoNetworkedField]
    public EngineeringGogglesMode Mode = EngineeringGogglesMode.Off;

    [DataField, AutoNetworkedField]
    public Color XRayColor = Color.FromHex("#FFA64099");

    [DataField, AutoNetworkedField]
    public Color TrayColor = Color.FromHex("#3D8599CC");

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundActivate = new SoundPathSpecifier("/Audio/_White/Items/Goggles/activate.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundDeactivate = new SoundPathSpecifier("/Audio/_White/Items/Goggles/deactivate.ogg");

    [DataField, AutoNetworkedField]
    public EntProtoId ToggleAction = "ActionToggleEngineeringGoggles";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;
}

[Serializable, NetSerializable]
public enum EngineeringGogglesMode : byte
{
    Off,
    XRay,
    Tray,
}

[Serializable, NetSerializable]
public enum EngineeringGogglesVisuals : byte
{
    Mode,
}
