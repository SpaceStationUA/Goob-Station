// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.ListeningPost.Systems;

namespace Content.Server._Pirate.ListeningPost.Components;

[RegisterComponent, Access(typeof(SyndicateDropChargeRuleSystem))]
public sealed partial class SyndicateDropChargeRuleComponent : Component
{
    [DataField]
    public int Charges = 1;
}
