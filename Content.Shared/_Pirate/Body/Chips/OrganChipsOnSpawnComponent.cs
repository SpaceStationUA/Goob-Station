// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Body.Chips;

[RegisterComponent]
public sealed partial class OrganChipsOnSpawnComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId<OrganChipComponent>> Chips = new();
}
