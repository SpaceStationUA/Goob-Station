// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.Shuttles.BUIStates; // Pirate - replay memory optimization.
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

/// <summary>
/// Wrapper around <see cref="NavInterfaceState"/>
/// </summary>
[Serializable, NetSerializable]
public sealed class NavBoundUserInterfaceState : BoundUserInterfaceState
{
    public NavInterfaceState State;
    public DockingPortStates DockingPortStates;

    public NavBoundUserInterfaceState(NavInterfaceState state, DockingPortStates dockingPortStates)
    {
        State = state;
        DockingPortStates = dockingPortStates;
    }
}
