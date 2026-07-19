// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors <https://github.com/AU-14/ColonialMarinesUniverse>
// SPDX-License-Identifier: AGPL-3.0-only
// Ported from ColonialMarinesUniverse Content.Client/_CMU14/ZLevels/Lighting/CMUProjectedLightComponent.cs.

using System.Numerics;
using Robust.Shared.Map;

namespace Content.Client._Pirate.ZLevels.Lighting;

/// <summary>
/// Marker for client-only projected light entities created by <see cref="CMUZLevelProjectedLightingSystem"/>.
/// These live on the receiving map and carry a regular <c>PointLightComponent</c> whose
/// parameters are derived from a source light on an adjacent Z-level.
/// </summary>
[RegisterComponent]
public sealed partial class CMUZProjectedLightComponent : Component
{
    /// <summary>The source light entity on the adjacent Z-level this projection represents.</summary>
    public EntityUid SourceLight;

    /// <summary>World-space center of the opening tile this projection is positioned at.</summary>
    public Vector2 OpeningCenter;

    /// <summary>Source map this projection came from, used for invalidation on Z-network change.</summary>
    public MapId SourceMapId;

    /// <summary>Depth offset from the viewer's Z-level (negative = below, positive = above).</summary>
    public int DepthOffset;

    /// <summary>Last frame this projected light was confirmed active; stale ones get deleted.</summary>
    public uint LastActiveFrame;

    /// <summary>Map this projected light was last positioned on.</summary>
    public MapId LastAppliedMapId = MapId.Nullspace;

    /// <summary>Last world position applied to the transform.</summary>
    public Vector2 LastAppliedCenter;
}
