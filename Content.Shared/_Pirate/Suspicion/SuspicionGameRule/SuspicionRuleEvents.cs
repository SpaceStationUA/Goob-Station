// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared._Pirate.Suspicion.SuspicionGameRule.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Suspicion.SuspicionGameRule;


[Serializable, NetSerializable]
public sealed class SuspicionRuleTimerUpdate(TimeSpan endTime) : EntityEventArgs
{
    public TimeSpan EndTime = endTime;
}

[Serializable, NetSerializable]
public sealed class SuspicionRulePreroundStarted(TimeSpan preroundEndTime) : EntityEventArgs
{
    public TimeSpan PreroundEndTime = preroundEndTime;
}

[Serializable, NetSerializable]
public sealed class SuspicionRuleUpdateRole(SuspicionRole newRole, SuspicionSubRole? newSubRole = null) : EntityEventArgs
{
    public readonly SuspicionRole NewRole = newRole;
    public readonly SuspicionSubRole? NewSubRole = newSubRole;
}

[Serializable, NetSerializable]
public sealed class SuspicionRulePlayerSpawn : EntityEventArgs
{
    public SuspicionGameState GameState;
    public TimeSpan EndTime;
}

[Serializable, NetSerializable]
public sealed class SuspicionHudClosed : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class SuspicionAllyRosterUpdated(NetEntity viewer, NetEntity[] traitors) : EntityEventArgs
{
    public readonly NetEntity Viewer = viewer;
    public readonly NetEntity[] Traitors = traitors;
}
