// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared._Pirate.Suspicion;
using Content.Shared._Pirate.Suspicion.SuspicionGameRule.Components;
using Content.Shared.Chat;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Storage;
using Content.Shared.Store.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Network;
using System.Diagnostics.CodeAnalysis;
namespace Content.Server._Pirate.Suspicion.SuspicionGameRule;

public sealed partial class SuspicionRuleSystem
{
    /// <summary>
    /// Sends the formatted result of a corpse inspection.
    /// </summary>
    private void SendAnnouncement(string message, Color? colorOverride = null)
    {
        _chatManager.ChatMessageToAll(
            ChatChannel.Server,
            message,
            message,
            EntityUid.Invalid,
            hideChat: false,
            recordReplay: true,
            colorOverride: colorOverride);
    }

    private void AddTcToPlayer(EntityUid player, int amount, bool displayMessage = true)
    {
        var implantedComponent = CompOrNull<ImplantedComponent>(player);
        if (implantedComponent == null)
            return;

        foreach (var implant in implantedComponent.ImplantContainer.ContainedEntities)
        {
            var storeComp = CompOrNull<StoreComponent>(implant);
            if (storeComp == null)
                continue;

            _storeSystem.TryAddCurrency(new Dictionary<string, FixedPoint2>()
                {
                    { "Telecrystal", FixedPoint2.New(amount)},
                },
                implant,
                storeComp
            );
        }

        if (!_playerManager.TryGetSessionByEntity(player, out var session))
            return;

        if (!displayMessage)
            return;

        var message = Loc.GetString("tc-added-sus", ("tc", amount));
        _chatManager.ChatMessageToOne(ChatChannel.Server, message, message, EntityUid.Invalid, false, session.Channel);
    }

    private Entity<StoreComponent>? GetUplinkImplant(EntityUid player)
    {
        var implantedComponent = CompOrNull<ImplantedComponent>(player);
        if (implantedComponent == null)
            return null;

        foreach (var implant in implantedComponent.ImplantContainer.ContainedEntities)
        {
            var storeComp = CompOrNull<StoreComponent>(implant);
            if (storeComp == null)
                continue;

            return (implant, storeComp);
        }

        return null;
    }

    /// <summary>
    /// Tries to find the SuspicionRoleComponent of a entity.
    /// </summary>
    private bool TrySusRole(EntityUid ent, [NotNullWhen(true)] out SuspicionRoleComponent? suspicionRole)
    {
        suspicionRole = null;

        if (!_mindSystem.TryGetMind(ent, out var mind, out var mindComp))
            return false;

        if (!_roleSystem.MindHasRole<SuspicionRoleComponent>(mind, out var susRole))
            return false;

        suspicionRole = susRole;
        return true;
    }

    /// <summary>
    /// Tries to find the SuspicionRoleComponent of a user.
    /// </summary>
    private bool TrySusRole(NetUserId userId, [NotNullWhen(true)] out SuspicionRoleComponent? suspicionRole)
    {
        suspicionRole = null;

        if (!_mindSystem.TryGetMind(userId, out var mind, out var mindComp) || mind == null)
            return false;

        var nullableMind = new Entity<MindComponent?>(mind.Value, mindComp);

        if (!_roleSystem.MindHasRole<SuspicionRoleComponent>(nullableMind, out var susRole))
            return false;

        suspicionRole = susRole;
        return true;
    }

    /// <summary>
    /// Finds all players with a specific role.
    /// </summary>
    private List<(EntityUid body, Entity<MindRoleComponent, SuspicionRoleComponent> sus)> FindAllOfType(SuspicionRole role, bool filterDead = true)
    {
        var result = new List<(EntityUid body, Entity<MindRoleComponent, SuspicionRoleComponent> sus)>();
        var players = EntityQueryEnumerator<SuspicionPlayerComponent>();
        while (players.MoveNext(out var body, out _))
        {
            if ((filterDead && _mobState.IsDead(body)) ||
                !_mindSystem.TryGetMind(body, out var mindId, out var mind) ||
                mind.OwnedEntity != body ||
                !_roleSystem.MindHasRole<SuspicionRoleComponent>(mindId, out var assigned) ||
                assigned.Value.Comp2.Role != role)
                continue;

            result.Add((body, assigned.Value));
        }
        return result;
    }

    public void DropAllItemsOnEntity(EntityUid entity)
    {
        if (!TryComp(entity, out InventoryComponent? inventory))
            return;

        var slots = _inventory.GetSlotEnumerator(new Entity<InventoryComponent?>(entity, inventory), SlotFlags.All);
        var targetPos = _transformSystem.GetWorldPosition(entity);

        while (slots.MoveNext(out var slot))
        {
            foreach (var rootContainerEnt in slot.ContainedEntities)
            {
                if (!TryComp(rootContainerEnt, out StorageComponent? storage))
                    continue;

                var dumpQueue = new Queue<EntityUid>(storage.Container.ContainedEntities);

                foreach (var item in dumpQueue)
                {
                    var transform = Transform(item);
                    _transformSystem.SetWorldPositionRotation(item, targetPos + _random.NextVector2Box() / 4, _random.NextAngle(), transform);
                }
            }
        }
    }

    public void AddKeyToRadio(EntityUid entity, string keyProto)
    {
        if (!_inventory.TryGetSlotEntity(entity, "ears", out var headset))
            return;

        if (!_containerSystem.TryGetContainer(headset.Value, "key_slots", out var container))
            return;

        var key = Spawn(keyProto, MapCoordinates.Nullspace);
        var transform = Comp<TransformComponent>(key);
        var meta = Comp<MetaDataComponent>(key);
        var physics = Comp<PhysicsComponent>(key);

        _containerSystem.Insert((key, transform, meta, physics), container, null, true);
    }
}
