// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors <https://github.com/AU-14/ColonialMarinesUniverse>
// SPDX-License-Identifier: AGPL-3.0-only
// Ported from CMU. Predicted-only twin of <see cref="CMUZLevelProjectileVisualOffsetComponent"/>:
// attached client-side during prediction so we don't dirty server-owned entities; replaced by
// the synced variant once the server confirms the shot.
using System.Numerics;

namespace Content.Shared._Pirate.ZLevels.Core.Components;

[RegisterComponent]
public sealed partial class CMUZLevelPredictedProjectileVisualOffsetComponent : Component
{
    /// <summary>Eye-independent barrel-shift; render compensation added client-side. See the synced twin.</summary>
    public Vector2 Offset;

    /// <summary>Shot Z offset (+1 up / -1 down).</summary>
    public int Depth;

    public Vector2? OriginalOffset;

    public Vector2 AppliedOffset;
}
