// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.VendingMachines;

[RegisterComponent]
public sealed partial class VendingPriceOverrideComponent : Component
{
    [DataField(required: true)]
    public Dictionary<EntProtoId, int> Prices = new();
}
