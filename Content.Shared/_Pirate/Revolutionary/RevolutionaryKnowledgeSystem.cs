// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Revolutionary.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Revolutionary;

public sealed class RevolutionaryKnowledgeSystem : EntitySystem
{
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;

    public static readonly EntProtoId RevolutionaryKnowledge = "RevolutionaryKnowledge";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevolutionaryComponent, MapInitEvent>(OnRevInit);
        SubscribeLocalEvent<RevolutionaryComponent, ComponentShutdown>(OnRevShutdown);
    }

    private void OnRevInit(Entity<RevolutionaryComponent> ent, ref MapInitEvent args)
    {
        if (_knowledge.GetContainer(ent.Owner) is not { } store)
            return;

        _knowledge.EnsureKnowledge(store, RevolutionaryKnowledge, 100, popup: false);
    }

    private void OnRevShutdown(Entity<RevolutionaryComponent> ent, ref ComponentShutdown args)
    {
        // Skip teardown when the entity is already being deleted.
        if (TerminatingOrDeleted(ent.Owner))
            return;

        _knowledge.RemoveKnowledge(ent.Owner, RevolutionaryKnowledge);
    }
}
