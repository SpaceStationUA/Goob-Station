// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Pirate.Suspicion;
using Content.Shared._Pirate.Suspicion.SuspicionGameRule;
using Content.Shared._Pirate.Suspicion.SuspicionGameRule.Components;
using Content.Shared.Ghost;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Suspicion.SuspicionGameRule;

public sealed partial class SuspicionRuleSystem
{
    private void SendAllyMarkersToViewers()
    {
        var traitors = GetTraitorBodies();
        foreach (var session in _playerManager.Sessions)
            SendAllyMarkers(session, traitors);
    }

    private void SendAllyMarkers(ICommonSession viewer, NetEntity[]? traitors = null)
    {
        if (ActiveRule?.Comp.GameState != SuspicionGameState.InProgress ||
            viewer.Status != SessionStatus.InGame || viewer.AttachedEntity is not {} entity)
            return;

        var seesTraitors = TryComp<GhostComponent>(entity, out var ghost)
            ? !ghost.CanReturnToBody
            : HasComp<SuspicionPlayerComponent>(entity) && TryGetOwnedRole(entity, viewer, out var role) &&
            role.Role == SuspicionRole.Traitor;

        // Direct session messages keep the roster out of public entity states and server replays.
        RaiseNetworkEvent(new SuspicionAllyRosterUpdated(GetNetEntity(entity),
            seesTraitors ? traitors ?? GetTraitorBodies() : []), viewer);
    }

    private NetEntity[] GetTraitorBodies()
        => FindAllOfType(SuspicionRole.Traitor, filterDead: false).Select(player => GetNetEntity(player.body)).ToArray();

    private bool TryGetOwnedRole(EntityUid body, ICommonSession viewer,
        [NotNullWhen(true)] out SuspicionRoleComponent? role)
    {
        role = null;
        if (!_mindSystem.TryGetMind(body, out var mindId, out var mind) ||
            mind.OwnedEntity != body || mind.UserId != viewer.UserId ||
            !_roleSystem.MindHasRole<SuspicionRoleComponent>(mindId, out var assigned))
            return false;

        role = assigned.Value.Comp2;
        return true;
    }
}
