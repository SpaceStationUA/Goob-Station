// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Newbies;
using Content.Server.NodeContainer.NodeGroups;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Database;
using Content.Shared.NodeContainer;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Atmos.Piping.Unary.EntitySystems;

public sealed partial class GasCanisterSystem
{
    private static readonly TimeSpan RequiredPlaytime = TimeSpan.FromHours(24);
    [Dependency] private readonly NovicePlaytimeSystem _novicePlaytime = default!;
    [Dependency] private readonly IGameTiming _noviceTiming = default!;

    protected override bool CanChangeReleaseValve(EntityUid uid, GasCanisterComponent canister, EntityUid actor, bool open)
    {
        if (!open || !TryComp<ActorComponent>(actor, out var player) ||
            !_novicePlaytime.IsRestricted(player.PlayerSession, RequiredPlaytime))
        {
            RemComp<NoviceCanisterComponent>(uid);
            return true;
        }

        if (!canister.GasTankSlot.HasItem && !HasConnectedPipe(uid, canister))
        {
            AlertNoviceRelease(uid, actor);
            return false;
        }

        EnsureComp<NoviceCanisterComponent>(uid).Opener = player.PlayerSession;
        return true;
    }

    protected override void BeforeHoldingTankEjected(EntityUid uid, GasCanisterComponent canister, EntityUid actor)
    {
        if (!canister.ReleaseValve || HasConnectedPipe(uid, canister) ||
            !TryComp<ActorComponent>(actor, out var player) ||
            !_novicePlaytime.IsRestricted(player.PlayerSession, RequiredPlaytime))
            return;

        CloseNoviceRelease(uid, canister);
        AlertNoviceRelease(uid, actor);
    }

    private bool HasConnectedPipe(EntityUid uid, GasCanisterComponent canister)
    {
        return Transform(uid).Anchored &&
               TryComp<NodeContainerComponent>(uid, out var nodes) &&
               _nodeContainer.TryGetNode(nodes, canister.PortName, out PortablePipeNode? port) &&
               port.ConnectionsEnabled && port.NodeGroup is PipeNet { NodeCount: > 1 };
    }

    private void EnforceNoviceRelease(EntityUid uid, GasCanisterComponent canister)
    {
        if (!canister.ReleaseValve || canister.GasTankSlot.HasItem || HasConnectedPipe(uid, canister) ||
            !TryComp<NoviceCanisterComponent>(uid, out var protection) || protection.Opener is not { } opener)
            return;

        if (!_novicePlaytime.IsRestricted(opener, RequiredPlaytime))
        {
            RemComp<NoviceCanisterComponent>(uid);
            return;
        }

        CloseNoviceRelease(uid, canister);
        if (opener.AttachedEntity is { } actor && !TerminatingOrDeleted(actor))
            AlertNoviceRelease(uid, actor);
    }

    private void CloseNoviceRelease(EntityUid uid, GasCanisterComponent canister)
    {
        canister.ReleaseValve = false;
        Dirty(uid, canister);
        DirtyUI(uid, canister);
        RemComp<NoviceCanisterComponent>(uid);
    }

    private void AlertNoviceRelease(EntityUid canister, EntityUid actor)
    {
        var cooldown = EnsureComp<NoviceCanisterAlertComponent>(actor);
        if (_noviceTiming.CurTime < cooldown.NextAlert)
            return;

        cooldown.NextAlert = _noviceTiming.CurTime + TimeSpan.FromSeconds(5);
        // Extreme produces the red admin alert, even while the player's playtime is unavailable.
        AdminLogger.Add(LogType.CanisterValve, LogImpact.Extreme,
            $"{ToPrettyString(actor):player} was blocked from releasing gas from {ToPrettyString(canister):canister} into the room: fewer than 24 hours of loaded playtime");
    }
}
