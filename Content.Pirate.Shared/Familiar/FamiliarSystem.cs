// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Common.Familiar;
using Content.Shared.Hands;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;

namespace Content.Pirate.Shared.Familiar;

/// <summary>
/// Tracks familiar/master relationships across ghost roles and minds.
/// </summary>
public sealed class FamiliarSystem : CommonFamiliarSystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    private EntityQuery<FamiliarMasterComponent> _familiarQuery;
    private EntityQuery<MindComponent> _mindQuery;

    public override void Initialize()
    {
        base.Initialize();

        _familiarQuery = GetEntityQuery<FamiliarMasterComponent>();
        _mindQuery = GetEntityQuery<MindComponent>();
        SubscribeLocalEvent<FamiliarMasterComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<PickupFamiliarComponent, GotEquippedHandEvent>(OnEquippedHand);
    }

    private void OnMindAdded(Entity<FamiliarMasterComponent> ent, ref MindAddedMessage args)
    {
        CopyMaster(ent.Owner, args.Mind.Owner);
    }

    private void OnEquippedHand(Entity<PickupFamiliarComponent> ent, ref GotEquippedHandEvent args)
    {
        SetMaster(ent.Owner, args.User);
        RemCompDeferred(ent, ent.Comp);
    }

    /// <inheritdoc />
    public override void SetMaster(EntityUid uid, EntityUid master)
    {
        var comp = EnsureComp<FamiliarMasterComponent>(uid);

        // A familiar cannot become the master of another familiar.
        if (CopyMaster(master, uid))
            return;

        if (_mind.GetMind(master) is { } mind)
            master = mind;

        if (comp.Master == master)
            return;

        comp.Master = master;
        comp.MasterName = GetName(master);
        Dirty(uid, comp);

        if (_mind.GetMind(uid) is { } familiarMind)
            CopyMaster(uid, familiarMind);
    }

    /// <summary>
    /// Copies a familiar relationship from one entity to another.
    /// </summary>
    public bool CopyMaster(EntityUid source, EntityUid destination)
    {
        // The relationship is normally stored on the source body. A mind entity is only
        // a fallback for callers that already pass the mind itself.
        if (!_familiarQuery.TryComp(source, out var sourceComp))
        {
            if (_mind.GetMind(source) is not { } sourceMind ||
                !_familiarQuery.TryComp(sourceMind, out sourceComp))
                return false;
        }

        var destinationComp = EnsureComp<FamiliarMasterComponent>(destination);
        destinationComp.Master = sourceComp.Master;
        destinationComp.MasterName = sourceComp.MasterName;
        Dirty(destination, destinationComp);
        return true;
    }

    /// <summary>
    /// Returns the displayed master name, or null for non-familiars.
    /// </summary>
    public string? GetMasterName(EntityUid uid)
    {
        if (_mind.GetMind(uid) is { } mind && GetMasterName(mind) is { } name)
            return name;

        return _familiarQuery.CompOrNull(uid)?.MasterName;
    }

    private string GetName(EntityUid uid)
        => _mindQuery.CompOrNull(uid)?.CharacterName ?? Name(uid);
}
