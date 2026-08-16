// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Enchanting.Components;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Enchanting;

/// <summary>
/// Applies a specific enchant to a compatible target, then consumes this entity.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(EnchantAdderSystem))]
public sealed partial class EnchantAdderComponent : Component
{
    [DataField(required: true)]
    public EntProtoId<EnchantComponent> Enchant;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Desc;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;
}
