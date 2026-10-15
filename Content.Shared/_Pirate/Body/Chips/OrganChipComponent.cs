// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Body.Chips;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrganChipComponent : Component
{
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField, AutoNetworkedField]
    public EntityUid? Organ;

    [DataField]
    public TimeSpan ShortDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan LongDelay = TimeSpan.FromSeconds(8);

    [DataField]
    public bool CanRemove = true;

    [DataField]
    public bool CanSelfRemove = true;

    // A family allows one chip per organ; unset families only block exact duplicates.
    [DataField]
    public ProtoId<OrganChipFamilyPrototype>? Family;

    // Hidden chips stay out of health analyzer scans.
    [DataField]
    public bool HiddenFromScanners;
}

[Serializable, NetSerializable]
public enum OrganChipVisuals : byte
{
    Disabled,
}

[Serializable, NetSerializable]
public enum OrganChipVisualLayers : byte
{
    Leds,
}

[ByRefEvent]
public record struct OrganChipInsertedEvent(EntityUid Organ, EntityUid? Body);

[ByRefEvent]
public record struct OrganChipRemovedEvent(EntityUid Organ, EntityUid? Body);
