// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Weapons.Ranged;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class VectorComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool StockFolded = true;

    [DataField]
    public SoundSpecifier FoldSound = new SoundPathSpecifier("/Audio/_Pirate/Weapons/Guns/Vector/wield.ogg");

    [DataField]
    public SoundSpecifier ExtendSound = new SoundPathSpecifier("/Audio/_Pirate/Weapons/Guns/Vector/wield.ogg");

    [DataField]
    public float DamageMultiplier = 0.7f;

    [DataField]
    public float ExtendedDamageBonus = 0.1f;

    [DataField]
    public float ExtendedSpeedMultiplier = 1f / 0.6f;

    [DataField]
    public float ExtendedPenetrationMultiplier = 1.2f;
}

[Serializable, NetSerializable]
public enum VectorVisuals : byte
{
    StockFolded,
    Base,
}
