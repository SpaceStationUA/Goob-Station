// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Common.Cyberdeck.Components;
using Content.Shared.Eye;
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Map;

namespace Content.Pirate.Shared.Cyberdeck;

public abstract partial class SharedCyberdeckSystem
{
    private void InitializeProjection()
    {
        SubscribeLocalEvent<CyberdeckUserComponent, CyberdeckVisionEvent>(OnCyberVisionUsed);
        SubscribeLocalEvent<CyberdeckUserComponent, CyberdeckVisionReturnEvent>(OnCyberVisionReturn);
        SubscribeLocalEvent<CyberdeckSiliconTargetComponent, GetVisMaskEvent>(OnSiliconGetVisMask);
        SubscribeLocalEvent<CyberdeckSiliconTargetComponent, MapInitEvent>(OnSiliconMapInit);
    }

    private static void OnSiliconGetVisMask(
        Entity<CyberdeckSiliconTargetComponent> ent,
        ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int) VisibilityFlags.StationAiNetwork;
    }

    private void OnSiliconMapInit(Entity<CyberdeckSiliconTargetComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || !TryComp(ent, out EyeComponent? eye))
            return;

        _eye.RefreshVisibilityMask((ent.Owner, eye));
    }

    private void AttachToProjection(Entity<CyberdeckUserComponent> user)
    {
        if (user.Comp.InProjection)
            return;

        EnsureComp<StationAiOverlayComponent>(user.Owner);
        EnsureComp<CyberdeckOverlayComponent>(user.Owner);

        _actions.AddAction(user.Owner, ref user.Comp.ReturnAction, user.Comp.ReturnActionId);
        _actions.RemoveAction(user.Owner, user.Comp.VisionAction);
        user.Comp.VisionAction = null;

        _audio.PlayLocal(user.Comp.DiveStartSound, user.Owner, user.Owner);

        if (user.Comp.ProjectionEntity == null || TerminatingOrDeleted(user.Comp.ProjectionEntity))
        {
            if (_net.IsClient)
                return;

            var projection = Spawn(user.Comp.ProjectionEntityId, MapCoordinates.Nullspace);
            var projectionComp = EnsureComp<CyberdeckProjectionComponent>(projection);
            projectionComp.RemoteEntity = user.Owner;
            user.Comp.ProjectionEntity = projection;

            Dirty(projection, projectionComp);
        }

        if (user.Comp.AiUiProxyEntity == null || TerminatingOrDeleted(user.Comp.AiUiProxyEntity))
        {
            if (_net.IsClient)
                return;

            user.Comp.AiUiProxyEntity = Spawn(user.Comp.AiUiProxyEntityId, MapCoordinates.Nullspace);
        }

        var projectionEntity = user.Comp.ProjectionEntity.Value;
        var proxyEntity = user.Comp.AiUiProxyEntity.Value;
        var proxy = EnsureComp<CyberdeckAiUiProxyComponent>(proxyEntity);
        proxy.RemoteEntity = user.Owner;
        proxy.TargetEntity = null;

        Xform.SetCoordinates(projectionEntity, Transform(user).Coordinates);
        Xform.SetCoordinates(proxyEntity, new EntityCoordinates(user.Owner, default));
        Dirty(proxyEntity, proxy);

        if (TryComp(user, out EyeComponent? eye))
        {
            _eye.SetDrawFov(user, false, eye);
            _eye.SetTarget(user, projectionEntity, eye);
            _eye.SetVisibilityMask(
                user,
                eye.VisibilityMask | (int) VisibilityFlags.StationAiNetwork,
                eye);
        }

        _mover.SetRelay(user, projectionEntity);
        user.Comp.InProjection = true;
        Dirty(user);
    }

    private void DetachFromProjection(Entity<CyberdeckUserComponent> user)
    {
        if (!user.Comp.InProjection)
            return;

        if (user.Comp.AiUiProxyEntity is { } proxyEntity
            && !TerminatingOrDeleted(proxyEntity)
            && TryComp(proxyEntity, out CyberdeckAiUiProxyComponent? proxy))
        {
            _ui.CloseUi(proxyEntity, AiUi.Key, user.Owner);
            proxy.TargetEntity = null;
            Dirty(proxyEntity, proxy);
        }

        RemComp<StationAiOverlayComponent>(user);
        RemComp<CyberdeckOverlayComponent>(user);

        _actions.AddAction(user, ref user.Comp.VisionAction, user.Comp.VisionActionId);
        _actions.RemoveAction(user.Owner, user.Comp.ReturnAction);
        user.Comp.ReturnAction = null;

        _audio.PlayLocal(user.Comp.DiveExitSound, user.Owner, user.Owner);

        if (TryComp(user, out EyeComponent? eye))
        {
            _eye.SetDrawFov(user, true, eye);
            _eye.SetTarget(user, null, eye);
            _eye.SetVisibilityMask(
                user,
                eye.VisibilityMask & ~(int) VisibilityFlags.StationAiNetwork,
                eye);
        }

        RemComp<RelayInputMoverComponent>(user);
        user.Comp.InProjection = false;
        Dirty(user);

        if (user.Comp.ProjectionEntity is not { } projection || TerminatingOrDeleted(projection))
            return;

        _cryostorage.EnsurePausedMap();
        if (_cryostorage.PausedMap == null)
        {
            Log.Error("Cryostorage paused map was unexpectedly null");
            return;
        }

        Xform.SetParent(projection, _cryostorage.PausedMap.Value);
    }

    private void OnCyberVisionUsed(Entity<CyberdeckUserComponent> ent, ref CyberdeckVisionEvent args)
    {
        if (args.Handled
            || HasComp<RelayInputMoverComponent>(ent.Owner)
            || !UseCharges(ent.Owner, ent.Comp.CyberVisionAbilityCost))
            return;

        AttachToProjection(ent);
        args.Handled = true;
    }

    private void OnCyberVisionReturn(Entity<CyberdeckUserComponent> ent, ref CyberdeckVisionReturnEvent args)
    {
        if (args.Handled)
            return;

        DetachFromProjection(ent);
        args.Handled = true;
    }
}
