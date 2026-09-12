// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

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
}

[ByRefEvent]
public record struct OrganChipInsertedEvent(EntityUid Organ, EntityUid? Body);

[ByRefEvent]
public record struct OrganChipRemovedEvent(EntityUid Organ, EntityUid? Body);
