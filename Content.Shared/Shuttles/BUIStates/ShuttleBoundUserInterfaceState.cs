// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Shuttles.BUIStates; // Pirate - replay memory optimization.
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class ShuttleBoundUserInterfaceState : BoundUserInterfaceState
{
    public NavInterfaceState NavState;
    public ShuttleMapInterfaceState MapState;
    public DockingInterfaceState DockState;
    public DockingPortStates DockingPortStates;

    public ShuttleBoundUserInterfaceState(
        NavInterfaceState navState,
        ShuttleMapInterfaceState mapState,
        DockingInterfaceState dockState,
        DockingPortStates dockingPortStates)
    {
        NavState = navState;
        MapState = mapState;
        DockState = dockState;
        DockingPortStates = dockingPortStates;
    }
}
