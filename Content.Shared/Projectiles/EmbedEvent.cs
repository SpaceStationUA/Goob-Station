// SPDX-FileCopyrightText: 2024 Dakamakat <52600490+dakamakat@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <comedian_vs_clown@hotmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

namespace Content.Shared.Projectiles;

/// <summary>
/// Raised directed on an entity when it embeds in another entity.
/// </summary>
[ByRefEvent]
public readonly record struct EmbedEvent(EntityUid? Shooter, EntityUid Embedded)
{
    public readonly EntityUid? Shooter = Shooter;

    /// <summary>
    /// Entity that is embedded in.
    /// </summary>
    public readonly EntityUid Embedded = Embedded;
}

/// <summary>
/// Raised directed on an entity when it stops being embedded in another entity.
/// </summary>
[ByRefEvent]
public readonly record struct EmbedDetachEvent(EntityUid? Detacher, EntityUid Embedded)
{
    /// <summary>
    /// The entity that detached the embed, if any.
    /// </summary>
    public readonly EntityUid? Detacher = Detacher;

    /// <summary>
    /// Entity that it is embedded in.
    /// </summary>
    public readonly EntityUid Embedded = Embedded;
}
