// SPDX-License-Identifier: MIT

using Content.Shared.Shuttles.Systems;
using Content.Shared.Shuttles.UI.MapObjects;
using Content.Shared.Timing;
using Content.Shared._Pirate.ZLevels.Shuttles; // Pirate: multiz
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

/// <summary>
/// Handles BUI data for Map screen.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShuttleMapInterfaceState
{
    /// <summary>
    /// The current FTL state.
    /// </summary>
    public readonly FTLState FTLState;

    /// <summary>
    /// When the current FTL state starts and ends.
    /// </summary>
    public StartEndTime FTLTime;

    public List<ShuttleBeaconObject> Destinations;

    public List<ShuttleExclusionObject> Exclusions;

    #region Pirate: multiz
    /// <summary>Current z-level traversal (fly up/down) phase.</summary>
    public CEZTraversalState ZTraversalState;

    /// <summary>When the current traversal phase started and ends, for the progress bar.</summary>
    public StartEndTime ZTraversalTime;

    /// <summary>Whether there is a level above the shuttle's top deck that it can fly into.</summary>
    public bool CanFlyUp;

    /// <summary>Whether there is a level below the shuttle's bottom deck that it can fly into.</summary>
    public bool CanFlyDown;
    #endregion

    public ShuttleMapInterfaceState(
        FTLState ftlState,
        StartEndTime ftlTime,
        List<ShuttleBeaconObject> destinations,
        List<ShuttleExclusionObject> exclusions)
    {
        FTLState = ftlState;
        FTLTime = ftlTime;
        Destinations = destinations;
        Exclusions = exclusions;
    }
}