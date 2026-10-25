// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.ActionBlocker;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Storage;

public sealed class SwordSheathSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SwordSheathComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<SwordSheathComponent, GetSmartEquipStorageItemEvent>(OnGetSmartEquipItem);
        SubscribeLocalEvent<SwordSheathComponent, EntInsertedIntoContainerMessage>(OnInserted,
            after: new[] { typeof(SharedStorageSystem) });
        SubscribeLocalEvent<SwordSheathComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnInserted(Entity<SwordSheathComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        // Network attachment order may differ; preserve server order.
        if (_timing.ApplyingState || args.Container.ID != StorageComponent.ContainerId
            || !args.Container.Contains(args.Entity))
            return;

        ent.Comp.RetrievalOrder.Remove(args.Entity);
        ent.Comp.RetrievalOrder.Add(args.Entity);
        Dirty(ent);
    }

    private void OnRemoved(Entity<SwordSheathComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState || args.Container.ID != StorageComponent.ContainerId)
            return;

        if (ent.Comp.RetrievalOrder.Remove(args.Entity))
            Dirty(ent);
    }

    private void OnGetSmartEquipItem(Entity<SwordSheathComponent> ent, ref GetSmartEquipStorageItemEvent args)
    {
        // Avoid local container order while replication is pending.
        args.Item = ent.Comp.RetrievalOrder.Count > 0 ? ent.Comp.RetrievalOrder[^1] : null;
    }

    private void OnGetAlternativeVerbs(Entity<SwordSheathComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null
            || !TryComp<StorageComponent>(ent, out var storage)
            || !CanDraw(ent, args.User))
            return;

        foreach (var item in storage.Container.ContainedEntities)
        {
            if (!_tag.HasTag(item, ent.Comp.SwordTag) || !_hands.CanPickupAnyHand(args.User, item))
                continue;

            var user = args.User;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("sheath-eject-verb"),
                IconEntity = GetNetEntity(item),
                Priority = 1,
                Act = () => DrawSword(ent, user, item),
            });
            return;
        }
    }

    private void DrawSword(Entity<SwordSheathComponent> ent, EntityUid user, EntityUid sword)
    {
        if (!TryComp<StorageComponent>(ent, out var storage)
            || !storage.Container.Contains(sword)
            || !_tag.HasTag(sword, ent.Comp.SwordTag)
            || !CanDraw(ent, user)
            || !_hands.TryPickupAnyHand(user, sword))
            return;

        if (!_tag.HasTag(user, storage.SilentStorageUserTag))
            _audio.PlayPredicted(storage.StorageRemoveSound, ent, user);
    }

    private bool CanDraw(EntityUid sheath, EntityUid user)
    {
        if (!_actionBlocker.CanInteract(user, sheath) || !_interaction.InRangeAndAccessible(user, sheath))
            return false;

        var attempt = new StorageInteractAttemptEvent(Silent: true);
        RaiseLocalEvent(sheath, ref attempt);
        return !attempt.Cancelled;
    }
}
