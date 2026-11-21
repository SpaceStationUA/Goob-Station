// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Network;

namespace Content.Server._Pirate.Administration;

// The Pirate server module owns the manual pacification list and its gameplay effects.
public sealed class PirateModerationPacifyEvent(NetUserId target, string username, int days) : EntityEventArgs
{
    public NetUserId Target { get; } = target;
    public string Username { get; } = username;
    public int Days { get; } = days;
    public bool Handled { get; set; }
}
