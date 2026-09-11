// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Body.Chips;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Knowledge;

public sealed class KnowledgeProfileSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly OrganChipSystem _chips = default!; // Pirate: skill chips
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent args)
    {
        // Restore pre-spawn grants after the profile rebuild.
        var species = _prototypes.Index<SpeciesPrototype>(args.Profile.Species);
        _knowledge.ApplyProfile(args.Mob, species.Knowledge, args.Profile.Knowledge);
        // Pirate: skill chips start
        _knowledge.ReplayCompetency(args.Mob);
        _chips.ReconcileInstalledChipModifiers(args.Mob);
        // Pirate: skill chips end
        _knowledge.ApplyEmployerBonuses(args.Mob, args.Profile.Employer);
    }
}
