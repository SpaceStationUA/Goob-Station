// SPDX-FileCopyrightText: 2025 Tyranex <bobthezombie4@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.MalfAI;
using Content.Shared._Pirate.MalfAI.Actions;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Content.Server.Power.Components;
using Content.Server.Actions;
using Content.Server.Administration.Logs;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._Pirate.MalfAI;

/// <summary>
/// Handles Malf AI shunting of the AI brain to APCs, and returning to the core.
/// Moves the entity with StationAiCoreComponent between container slots on the core/APC holders.
/// </summary>
public sealed class MalfAiShuntSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly Content.Server.Silicons.StationAi.StationAiSystem _stationAi = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Subscribe on the AI performer entity.
        SubscribeLocalEvent<StationAiHeldComponent, MalfAiShuntToApcActionEvent>(OnShuntToApc);
        SubscribeLocalEvent<StationAiHeldComponent, MalfAiReturnToCoreActionEvent>(OnReturnToCore);
    }

    private void OnShuntToApc(Entity<StationAiHeldComponent> ai, ref MalfAiShuntToApcActionEvent args)
    {
        var popupTarget = GetAiEyeForPopup(ai.Owner) ?? ai.Owner;

        // Only Malf AI can shunt.
        if (!HasComp<MalfAiMarkerComponent>(ai))
            return;

        if (!HasComp<ApcComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("malfai-shunt-invalid-target"), popupTarget, ai);
            return;
        }
        var target = args.Target;

        // Ensure the AI is currently inside a holder (core or other) and get that holder.
        if (!_containers.TryGetContainingContainer((ai.Owner, null, null), out var currentContainer) || currentContainer is not ContainerSlot)
        {
            _popup.PopupEntity(Loc.GetString("malfai-shunt-no-holder"), popupTarget, ai);
            return;
        }

        var currentHolder = currentContainer.Owner;

        // APCs are not StationAiCore entities by default. The shared Station AI lifecycle
        // owns the eye/PVS relay, so give this temporary holder the same core component
        // before insertion; otherwise removing the brain from the real core clears the eye
        // and leaves the player with no visible destination or input relay.
        var addedApcHolder = !HasComp<StationAiHolderComponent>(target);
        EnsureComp<StationAiHolderComponent>(target);
        var destContainer = _containers.EnsureContainer<ContainerSlot>(target, StationAiHolderComponent.Container);
        var addedApcCore = !HasComp<StationAiCoreComponent>(target);
        EnsureComp<StationAiCoreComponent>(target);

        // Edge case popup for if there's another AI occupying the APC.
        if (destContainer.ContainedEntities.Count != 0)
        {
            if (addedApcCore)
                RemCompDeferred<StationAiCoreComponent>(target);
            if (addedApcHolder)
                RemCompDeferred<StationAiHolderComponent>(target);
            _popup.PopupEntity(Loc.GetString("malfai-shunt-apc-occupied"), popupTarget, ai);
            return;
        }

        // Preserve the original core and remember any temporary APC holder being left.
        var shunted = EnsureComp<MalfAiShuntedComponent>(ai);
        var previousHolder = currentHolder;
        var previousAddedApcHolder = shunted.AddedApcHolder;
        var previousAddedApcCore = shunted.AddedApcCore;
        if (shunted.CoreHolder == null)
            shunted.CoreHolder = currentHolder;

        // Move the AI brain to the APC. Roll back if the container rejects the transfer.
        _containers.Remove(ai.Owner, currentContainer);
        if (!_containers.Insert(ai.Owner, destContainer))
        {
            _containers.Insert(ai.Owner, currentContainer);
            if (shunted.CoreHolder == currentHolder && shunted.ReturnAction == null)
                RemCompDeferred<MalfAiShuntedComponent>(ai);
            if (addedApcCore)
                RemCompDeferred<StationAiCoreComponent>(target);
            if (addedApcHolder)
                RemCompDeferred<StationAiHolderComponent>(target);
            _popup.PopupEntity(Loc.GetString("malfai-shunt-invalid-target"), popupTarget, ai);
            return;
        }
        if (previousHolder != target && HasComp<ApcComponent>(previousHolder))
        {
            if (previousAddedApcCore)
                RemCompDeferred<StationAiCoreComponent>(previousHolder);
            if (previousAddedApcHolder)
                RemCompDeferred<StationAiHolderComponent>(previousHolder);
        }
        shunted.AddedApcHolder = addedApcHolder;
        shunted.AddedApcCore = addedApcCore;

        // Ensure the AI stays marked as held.
        EnsureComp<StationAiHeldComponent>(ai);

        // Close any viewport while the AI is in a local APC eye.
        if (TryComp<MalfAiViewportComponent>(ai, out var comp))
            comp.Selected = null;

        if (TryComp<ActorComponent>(ai, out var actor) && actor.PlayerSession != null)
            RaiseNetworkEvent(new MalfAiViewportCloseEvent(), actor.PlayerSession);

        // Grant Return to Core action while shunted, and remember the action entity for removal.
        if (shunted.ReturnAction == null)
        {
            var returnAction = _actions.AddAction(ai.Owner, "ActionMalfAiReturnToCore");
            if (returnAction != null)
                shunted.ReturnAction = returnAction.Value;
        }
        _popup.PopupEntity(Loc.GetString("malfai-shunt-success"), GetAiEyeForPopup(ai.Owner) ?? ai.Owner, ai);
        args.Handled = true;
    }


    private void OnReturnToCore(Entity<StationAiHeldComponent> ai, ref MalfAiReturnToCoreActionEvent args)
    {
        if (args.Handled)
            return;

        var popupTarget = GetAiEyeForPopup(ai.Owner) ?? ai.Owner;

        // Only Malf AI can return via this action.
        if (!HasComp<MalfAiMarkerComponent>(ai))
            return;

        // If the AI is currently hijacking a mech, delegate to hijack system instead.
        if (HasComp<MalfAiMechHijackComponent>(ai))
        {
            var hijackSys = EntityManager.System<MalfAiHijackMechSystem>();
            hijackSys.ReturnFromHijack(ai.Owner);
            args.Handled = true;
            return;
        }

        if (!TryComp<MalfAiShuntedComponent>(ai, out var shunted) || shunted.CoreHolder == null)
        {
            _popup.PopupEntity(Loc.GetString("malfai-return-no-core"), popupTarget, ai);
            return;
        }

        // Ensure we are currently in a container (e.g., in an APC).
        if (!_containers.TryGetContainingContainer((ai.Owner, null, null), out var currentContainer) || currentContainer is not ContainerSlot)
        {
            _popup.PopupEntity(Loc.GetString("malfai-return-not-shunted"), popupTarget, ai);
            return;
        }

        // Validate core holder: if missing or invalid, eject to floor with message.
        var coreHolder = shunted.CoreHolder.Value;
        if (Deleted(coreHolder) || !HasComp<StationAiCoreComponent>(coreHolder))
        {
            _containers.Remove(ai.Owner, currentContainer);
            _transform.DropNextTo(ai.Owner, currentContainer.Owner);
            CleanupShuntAction(shunted);
            CleanupApcHolder(currentContainer.Owner, shunted);
            shunted.CoreHolder = null;
            RemCompDeferred<MalfAiShuntedComponent>(ai);
            _popup.PopupEntity("Core not found!", popupTarget, ai);
            args.Handled = true;
            return;
        }

        // Ensure the core still has/gets the correct container.
        var coreContainer = _containers.EnsureContainer<ContainerSlot>(coreHolder, StationAiHolderComponent.Container);

        // If the core slot is occupied, we cannot return.
        if (coreContainer.ContainedEntities.Count != 0)
        {
            _popup.PopupEntity(Loc.GetString("malfai-return-core-occupied"), popupTarget, ai);
            return;
        }

        // Move back into the core holder atomically.
        var previousHolder = currentContainer.Owner;
        _containers.Remove(ai.Owner, currentContainer);
        if (!_containers.Insert(ai.Owner, coreContainer))
        {
            _containers.Insert(ai.Owner, currentContainer);
            _popup.PopupEntity(Loc.GetString("malfai-return-core-occupied"), popupTarget, ai);
            return;
        }

        EnsureComp<StationAiHeldComponent>(ai);
        CleanupApcHolder(previousHolder, shunted);
        CleanupShuntAction(shunted);
        shunted.CoreHolder = null;
        RemCompDeferred<MalfAiShuntedComponent>(ai);

        _popup.PopupEntity(Loc.GetString("malfai-return-success"), GetAiEyeForPopup(ai.Owner) ?? ai.Owner, ai);
        args.Handled = true;
    }

    private void CleanupShuntAction(MalfAiShuntedComponent shunted)
    {
        if (shunted.ReturnAction is not { } action)
            return;
        _actions.RemoveAction(action);
        shunted.ReturnAction = null;
    }

    private void CleanupApcHolder(EntityUid holder, MalfAiShuntedComponent shunted)
    {
        if (!HasComp<ApcComponent>(holder))
            return;

        if (shunted.AddedApcCore)
            RemCompDeferred<StationAiCoreComponent>(holder);
        if (shunted.AddedApcHolder)
            RemCompDeferred<StationAiHolderComponent>(holder);
        shunted.AddedApcCore = false;
        shunted.AddedApcHolder = false;
    }

    /// <summary>
    /// Gets the AI eye entity for popup positioning, falls back to core if eye unavailable
    /// </summary>
    private EntityUid? GetAiEyeForPopup(EntityUid aiUid)
    {
        if (!_stationAi.TryGetCore(aiUid, out var core) || core.Comp?.RemoteEntity == null)
            return null;

        return core.Comp.RemoteEntity.Value;
    }
}
