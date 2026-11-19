// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Clothing.Accessories;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(ArmbandSideSystem))]
public sealed partial class ArmbandSideComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Flipped;

    [DataField]
    public string Prefix = "flipped";
}

[Serializable, NetSerializable]
public enum ArmbandSideVisuals : byte
{
    Flipped,
}

[Serializable, NetSerializable]
public enum ArmbandSideLayers : byte
{
    Base,
}
