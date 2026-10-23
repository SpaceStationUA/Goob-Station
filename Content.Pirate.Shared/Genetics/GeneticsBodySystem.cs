// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Pirate.Shared.Genetics.Mutations;
using Robust.Shared.Network;
using Robust.Shared.Containers;

namespace Content.Pirate.Shared.Genetics;

/// <summary>
/// Pirate: maps Trauma's organ categories onto our separate body-part and organ slots.
/// Keeps genetics effects independent of Trauma's replacement body implementation.
/// </summary>
public sealed class GeneticsBodySystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly INetManager _net = default!;

    // Pirate: local bodies have no tongue slots. Supply the donor organ for genetics,
    // before default mutations are activated, without replacing the body implementation.
    [SubscribeLocalEvent(after: [typeof(SharedBodySystem)], before: [typeof(MutationSystem)])]
    private void OnMapInit(Entity<MutatableComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || GetOrgan(ent, "Head") is not {} head || GetOrgan(ent, "Tongue") != null)
            return;
        _body.TryCreateOrganSlot(head, "tongue", out _);
        var reptilian = TryComp<HumanoidAppearanceComponent>(ent, out var appearance) &&
                        appearance.Species == "Reptilian";
        PredictedSpawnInContainerOrDrop(reptilian ? "OrganReptilianTongue" : "OrganHumanTongue",
            head, SharedBodySystem.GetOrganContainerId("tongue"));
    }

    public IEnumerable<Entity<BodyPartComponent>> GetBodyParts(EntityUid body,
        BodyPartType? type = null, BodyPartSymmetry? symmetry = null)
    {
        // Effects can detach parts while iterating.
        foreach (var (uid, part) in _body.GetBodyChildren(body).ToArray())
        {
            if ((type == null || part.PartType == type) && (symmetry == null || part.Symmetry == symmetry))
                yield return (uid, part);
        }
    }

    public EntityUid? GetOrgan(EntityUid body, string category)
    {
        if (Enum.TryParse<BodyPartType>(category, out var type))
        {
            foreach (var part in GetBodyParts(body, type))
                return part.Owner;
        }

        var slot = category.ToLowerInvariant();
        foreach (var (uid, organ) in _body.GetBodyOrgans(body))
        {
            if (organ.SlotId == slot)
                return uid;
        }
        return null;
    }

    public bool HasOrganSlot(Entity<BodyPartComponent> part, string category)
    {
        var slot = category.ToLowerInvariant();
        return part.Comp.Organs.ContainsKey(slot) || part.Comp.Children.ContainsKey(slot);
    }

    public bool TryAddSlot(EntityUid uid, string category)
    {
        var slot = category.ToLowerInvariant();
        return Enum.TryParse<BodyPartType>(category, out var type)
            ? _body.TryCreatePartSlot(uid, slot, type, BodyPartSymmetry.None, out _)
            : _body.TryCreateOrganSlot(uid, slot, out _);
    }

    public bool TryRemoveSlot(EntityUid uid, string category)
    {
        if (!TryComp<BodyPartComponent>(uid, out var part))
            return false;
        var slot = category.ToLowerInvariant();
        var partSlot = part.Children.ContainsKey(slot);
        var containerId = partSlot ? SharedBodySystem.GetPartSlotContainerId(slot) : SharedBodySystem.GetOrganContainerId(slot);
        if (_containers.TryGetContainer(uid, containerId, out var container) && container.ContainedEntities.Count != 0)
            return false;
        var removed = partSlot ? part.Children.Remove(slot) : part.Organs.Remove(slot);
        if (removed)
            Dirty(uid, part);
        return removed;
    }

    public bool RemoveOrgan(EntityUid body, EntityUid organ) => Detach(organ);

    public bool Detach(EntityUid organ)
    {
        if (HasComp<OrganComponent>(organ))
            return _body.RemoveOrgan(organ);
        if (!HasComp<BodyPartComponent>(organ) || !_containers.TryGetContainingContainer((organ, null, null), out var container))
            return false;
        return _containers.Remove(organ, container);
    }

    public bool MoveOrgan(EntityUid body, string category, string destination)
    {
        if (GetOrgan(body, category) is not {} organ || GetOrgan(body, destination) is not {} dest)
            return false;
        var slot = category.ToLowerInvariant();
        // Regrown heads get their native slots from the body prototype; the extra
        // genetics tongue slot must also be restored before moving the tongue back.
        if (category == "Tongue")
            _body.TryCreateOrganSlot(dest, slot, out _);
        if (!_body.CanInsertOrgan(dest, slot) ||
            !_containers.TryGetContainer(dest, SharedBodySystem.GetOrganContainerId(slot), out var target) ||
            !_containers.CanInsert(organ, target))
            return false;
        // Insertion moves directly between containers and preserves the brain/mind entity.
        return _body.InsertOrgan(dest, organ, slot);
    }

    public void RestoreInitialChild(EntityUid parent, string category, bool recursive = true)
    {
        if (!TryComp<BodyPartComponent>(parent, out var part) || part.Body is not {} body ||
            !TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Prototype is not {} bodyId)
            return;
        var slot = category.ToLowerInvariant();
        var prototype = ProtoMan.Index(bodyId);
        if (!part.Children.ContainsKey(slot) || !prototype.Slots.TryGetValue(slot, out var initial) ||
            initial.Part is not {} partId ||
            !_containers.TryGetContainer(parent, SharedBodySystem.GetPartSlotContainerId(slot), out var container) ||
            container.ContainedEntities.Count != 0)
            return;

        var child = PredictedSpawnAtPosition(partId, Transform(body).Coordinates);
        if (!_body.AttachPart(parent, slot, child))
        {
            PredictedQueueDel(child);
            return;
        }
        // Nonrecursive regrowth still needs empty slots for the original organs to be moved back.
        foreach (var (organSlot, organId) in initial.Organs)
        {
            _body.TryCreateOrganSlot(child, organSlot, out _);
            if (recursive)
                PredictedSpawnInContainerOrDrop(organId, child, SharedBodySystem.GetOrganContainerId(organSlot));
        }
        if (!recursive)
            return;
        foreach (var connection in initial.Connections)
        {
            if (!prototype.Slots.TryGetValue(connection, out var next) || next.Part is not {} nextId)
                continue;
            var nextPart = ProtoMan.Index(nextId).Components["BodyPart"].Component as BodyPartComponent;
            if (nextPart == null)
                continue;
            _body.TryCreatePartSlot(child, connection, nextPart.PartType, nextPart.Symmetry, out _);
            RestoreInitialChild(child, connection, true);
        }
    }
}
