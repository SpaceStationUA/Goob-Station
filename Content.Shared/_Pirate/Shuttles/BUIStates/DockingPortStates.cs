// SPDX-FileCopyrightText: 2026 Red Mushie <noreply@redmushie.me>
//
// SPDX-License-Identifier: MIT

// Pirate port: Starlight replay memory optimization.
using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Shuttles.BUIStates;

/// <summary>
/// Wrapper for docking port data shared by shuttle-related interface states.
/// </summary>
[Serializable, NetSerializable]
public sealed class DockingPortStates
{
    public Dictionary<NetEntity, List<DockingPortState>> Docks;

    public DockingPortStates(Dictionary<NetEntity, List<DockingPortState>> docks)
    {
        Docks = docks;
    }
}
