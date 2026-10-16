// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Objectives.Systems;

namespace Content.Server._Pirate.Objectives.Components;

/// <summary>
/// Tracks crew casualties after a mass-casualty contract is accepted.
/// </summary>
[RegisterComponent, Access(typeof(MassCasualtyConditionSystem))]
public sealed partial class MassCasualtyConditionComponent : Component
{
    [DataField]
    public int Target = 8;

    [DataField]
    public int MinimumCrew = 20;

    [DataField]
    public bool Active;

    [DataField]
    public EntityUid? Map;

    [DataField]
    public EntityUid? OwnerMind;

    /// <summary>
    /// Minds observed alive since the contract was accepted. Reviving a victim removes their progress.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> Targets = new();
}
