// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Knowledge;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Knowledge;

/// <summary>
/// Applies the saved species and character skill profile after the final mob is spawned.
/// </summary>
public sealed class KnowledgeProfileSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent args)
    {
        var species = _prototypes.Index<SpeciesPrototype>(args.Profile.Species);
        _knowledge.ApplyProfile(args.Mob, species.Knowledge, args.Profile.Knowledge);
        _knowledge.ApplyEmployerBonuses(args.Mob, args.Profile.Employer);
    }
}
