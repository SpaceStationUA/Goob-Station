// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Revolutionary.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Revolutionary;

public sealed class RevolutionaryKnowledgeSystem : EntitySystem
{
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;

    public static readonly EntProtoId RevolutionaryKnowledge = "RevolutionaryKnowledge";

    public const int HeadLevel = 100;

    public const int LieutenantLevel = 88;

    public const int RevLevel = 75;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevolutionaryComponent, MapInitEvent>(OnRevInit);
        SubscribeLocalEvent<HeadRevolutionaryComponent, MapInitEvent>(OnHeadRevInit);

    }

    private void OnRevInit(Entity<RevolutionaryComponent> ent, ref MapInitEvent args)
        => Grant(ent.Owner, RevLevel);

    private void OnHeadRevInit(Entity<HeadRevolutionaryComponent> ent, ref MapInitEvent args)
        => Grant(ent.Owner, HeadLevel);

    public void Grant(EntityUid uid, int level)
    {
        if (_knowledge.GetContainer(uid) is not { } store)
            return;

        _knowledge.EnsureKnowledge(store, RevolutionaryKnowledge, level, popup: false);
    }

    public void SetRank(EntityUid uid, int level)
    {
        if (TerminatingOrDeleted(uid) || _knowledge.GetContainer(uid) is null)
            return;

        _knowledge.SetKnowledgeProgress(uid, RevolutionaryKnowledge, level, 0);
    }

    public void Forget(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return;

        _knowledge.RemoveKnowledge(uid, RevolutionaryKnowledge);
    }
}
