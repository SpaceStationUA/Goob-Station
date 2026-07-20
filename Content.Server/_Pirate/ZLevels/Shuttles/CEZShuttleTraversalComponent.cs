using Content.Shared._Pirate.ZLevels.Shuttles;
using Content.Shared.Timing;

namespace Content.Server._Pirate.ZLevels.Shuttles;

/// <summary>
/// Runtime state for an in-progress shuttle z-level traversal. Lives on the root (depth-0) shuttle
/// grid for the duration of a fly up/down and is removed once the exit cooldown finishes.
/// </summary>
[RegisterComponent]
public sealed partial class CEZShuttleTraversalComponent : Component
{
    /// <summary>Current phase of the traversal.</summary>
    [ViewVariables]
    public CEZTraversalState State = CEZTraversalState.Starting;

    /// <summary>When the current phase started and ends.</summary>
    [ViewVariables]
    public StartEndTime StateTime;

    /// <summary>Direction of travel: +1 = up, -1 = down.</summary>
    [ViewVariables]
    public int Direction;
}
