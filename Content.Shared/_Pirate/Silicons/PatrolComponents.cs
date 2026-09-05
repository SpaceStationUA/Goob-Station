// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Silicons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PatrolCommanderComponent : Component
{
    [DataField]
    public EntProtoId WaypointId = "SecuritronWaypoint";

    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Waypoints = [];

    [DataField, AutoNetworkedField]
    public bool IsPatrolling;

    [DataField]
    public SoundSpecifier EnslaveSound = new SoundPathSpecifier("/Audio/Machines/chime.ogg");
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PatrolSlaveComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? MasterEntity;
}

/// <summary>
/// Spatial marker used to distinguish commander-created patrol waypoints.
/// </summary>
[RegisterComponent]
public sealed partial class PatrolWaypointComponent : Component;

public sealed partial class TogglePatrolActionEvent : InstantActionEvent;
public sealed partial class PatrolWaypointActionEvent : WorldTargetActionEvent;
public sealed partial class ClearPatrolWaypointsActionEvent : InstantActionEvent;
