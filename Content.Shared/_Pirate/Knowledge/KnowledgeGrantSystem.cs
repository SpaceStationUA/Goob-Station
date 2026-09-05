// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Knowledge;

/// <summary>
/// Event-driven sources of knowledge, such as books and explicit prototype grants.
/// </summary>
public sealed class KnowledgeGrantSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _network = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KnowledgeGrantComponent, MapInitEvent>(OnGrantMapInit,
            after: [typeof(SharedKnowledgeSystem)]);
        SubscribeLocalEvent<KnowledgeGrantOnUseComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<KnowledgeGrantOnUseComponent, KnowledgeLearnDoAfterEvent>(OnLearnDoAfter);
        SubscribeLocalEvent<KnowledgeConflictComponent, KnowledgeAddedEvent>(OnConflictAdded);
    }

    private void OnGrantMapInit(Entity<KnowledgeGrantComponent> ent, ref MapInitEvent args)
    {
        _knowledge.AddKnowledgeUnits(ent.Owner, ent.Comp.Skills);
        RemCompDeferred<KnowledgeGrantComponent>(ent.Owner);
    }

    private void OnUseInHand(Entity<KnowledgeGrantOnUseComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.Instant ? TimeSpan.Zero : ent.Comp.DoAfter,
            new KnowledgeLearnDoAfterEvent(),
            ent.Owner,
            target: ent.Owner,
            used: ent.Owner)
        {
            BreakOnDropItem = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
        args.Handled = true;
    }

    private void OnLearnDoAfter(Entity<KnowledgeGrantOnUseComponent> ent, ref KnowledgeLearnDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target || TerminatingOrDeleted(target))
            return;

        if (!_timing.IsFirstTimePredicted || _knowledge.GetContainer(args.User) is not { } store)
            return;

        args.Handled = true;

        if (ent.Comp.Instant)
        {
            foreach (var (id, level) in ent.Comp.Skills)
                _knowledge.EnsureKnowledge(store, id, level);

            if (ent.Comp.GrantEverything)
            {
                foreach (var id in _knowledge.AllKnowledges.Keys)
                    _knowledge.EnsureKnowledge(store, id, 100);
            }

            if (ent.Comp.SingleUse)
            {
                PredictedQueueDel(ent.Owner);
                PredictedSpawnNextToOrDrop(ent.Comp.Ash, args.User);
            }

            return;
        }

        var learned = false;
        foreach (var (id, experience) in ent.Comp.Experience)
        {
            if (_knowledge.EnsureKnowledge(store, id) is not { } skill)
                continue;

            var cap = ent.Comp.Skills.GetValueOrDefault(id, 100);
            if (cap >= 0 && skill.Comp.LearnedLevel >= cap)
                continue;

            learned = true;
            _knowledge.AddExperience(skill, args.User, experience, cap < 0 ? 100 : cap);
        }

        args.Repeat = learned;
        if (!learned)
        {
            _popup.PopupClient(
                Loc.GetString("knowledge-could-not-learn"),
                args.User,
                args.User,
                PopupType.SmallCaution);
        }

        if (_network.IsClient)
        {
            var changed = new KnowledgeExperienceChangedEvent();
            RaiseLocalEvent(args.User, ref changed);
        }
    }

    private void OnConflictAdded(Entity<KnowledgeConflictComponent> ent, ref KnowledgeAddedEvent args)
    {
        foreach (var conflict in ent.Comp.Conflicts)
            _knowledge.RemoveKnowledge(args.Holder, conflict, force: true);
    }
}

[Serializable, NetSerializable]
public sealed partial class KnowledgeLearnDoAfterEvent : SimpleDoAfterEvent;
