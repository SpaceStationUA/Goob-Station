// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Pirate.Administration;

public sealed record ManualPacification(Guid Target, string Name, long Expires, string Version);

public sealed class PiratePacificationReviewEvent : EntityEventArgs
{
    public IReadOnlyList<ManualPacification>? Active { get; set; }
}

public sealed class PiratePardonPacificationEvent(Guid target, string version) : EntityEventArgs
{
    public Guid Target { get; } = target;
    public string Version { get; } = version;
    public string? State { get; set; }
}
