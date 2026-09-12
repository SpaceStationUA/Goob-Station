// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Pirate.Body.Chips;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared._Pirate.Knowledge;
using Content.Shared._Pirate.Roles;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Knowledge;

public sealed class KnowledgeProfileSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly OrganChipSystem _chips = default!; // Pirate: skill chips
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;

    private readonly List<EntityUid> _pendingBaseline = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<HumanoidAppearanceComponent, MindAddedMessage>(OnMindAdded);
        SubscribeNetworkEvent<KnowledgeJobChipsRequest>(OnJobChipsRequest);
    }

    private void OnMindAdded(Entity<HumanoidAppearanceComponent> ent, ref MindAddedMessage args)
    {
        // Defer until normal spawn processing has run.
        _pendingBaseline.Add(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingBaseline.Count == 0)
            return;

        foreach (var uid in _pendingBaseline)
        {
            if (TerminatingOrDeleted(uid) ||
                _knowledge.GetContainer(uid) is not { } store ||
                store.Comp.ProfileApplied ||
                !TryComp<HumanoidAppearanceComponent>(uid, out var humanoid) ||
                !_prototypes.TryIndex<SpeciesPrototype>(humanoid.Species, out var species))
            {
                continue;
            }

            _knowledge.ApplyProfile(uid, species.Knowledge, new KnowledgeProfile());

            // Restore grants lost when ApplyProfile rebuilds the store.
            _knowledge.ReplayCompetency(uid);
            _chips.ReconcileInstalledChipModifiers(uid);
        }

        _pendingBaseline.Clear();
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

    private void OnJobChipsRequest(KnowledgeJobChipsRequest args, EntitySessionEventArgs session)
    {
        var mappings = new Dictionary<string, string[]>();
        foreach (var job in _prototypes.EnumeratePrototypes<JobPrototype>())
        {
            var chips = job.Special
                .OfType<OrganChipsSpecial>()
                .SelectMany(special => special.Chips)
                .Select(id => id.Id)
                .ToArray();

            if (chips.Length > 0)
                mappings[job.ID] = chips;
        }

        RaiseNetworkEvent(new KnowledgeJobChipsResponse(mappings), session.SenderSession);
    }
}
