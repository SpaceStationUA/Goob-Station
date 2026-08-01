// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Whitelist;

namespace Content.Server._Pirate.Objectives;

[RegisterComponent]
public sealed partial class EnsureLawBoundEntitiesHaveNoLawsConditionComponent : Component
{
    [DataField]
    public int EntitiesToFree = 3;

    [DataField]
    public EntityWhitelist? LawEntityWhitelist;

    [DataField]
    public EntityWhitelist? LawEntityBlacklist;
}
