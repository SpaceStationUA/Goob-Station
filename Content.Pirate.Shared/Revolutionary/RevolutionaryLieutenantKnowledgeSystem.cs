// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Revolutionary.Components;
using Content.Shared._Pirate.Revolutionary;
using Content.Shared.Revolutionary.Components;

namespace Content.Pirate.Shared.Revolutionary;

public sealed class RevolutionaryLieutenantKnowledgeSystem : EntitySystem
{
    [Dependency] private readonly RevolutionaryKnowledgeSystem _revKnowledge = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevolutionaryLieutenantComponent, MapInitEvent>(OnLieutenantInit);
        SubscribeLocalEvent<RevolutionaryLieutenantComponent, ComponentShutdown>(OnLieutenantShutdown);
    }

    private void OnLieutenantInit(Entity<RevolutionaryLieutenantComponent> ent, ref MapInitEvent args)
        => _revKnowledge.Grant(ent.Owner, RevolutionaryKnowledgeSystem.LieutenantLevel);

    private void OnLieutenantShutdown(Entity<RevolutionaryLieutenantComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return;

        // Do not demote head revolutionaries.
        if (HasComp<HeadRevolutionaryComponent>(ent.Owner))
            return;

        // Deconversion clears the knowledge separately.
        if (!HasComp<RevolutionaryComponent>(ent.Owner))
            return;

        _revKnowledge.SetRank(ent.Owner, RevolutionaryKnowledgeSystem.RevLevel);
    }
}
