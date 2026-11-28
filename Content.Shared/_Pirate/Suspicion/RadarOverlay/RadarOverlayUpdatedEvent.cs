// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Suspicion.RadarOverlay;

/// <summary>
/// Holds the information for the traitor and detective radar.
/// </summary>
[Serializable, NetSerializable]
public sealed class RadarOverlayUpdatedEvent : EntityEventArgs
{
    public readonly RadarInfo[] RadarInfos;

    public RadarOverlayUpdatedEvent(RadarInfo[] radarInfos)
    {
        RadarInfos = radarInfos;
    }
}

[Serializable, NetSerializable]
public sealed class RadarInfo
{
    /// <summary>
    /// The color of the radar blip.
    /// </summary>
    public readonly Color Color;

    public readonly Vector2 Position;

    public RadarInfo(Color color, Vector2 position)
    {
        Color = color;
        Position = position;
    }
}


[Serializable, NetSerializable]
public sealed class OnRadarOverlayToggledEvent : EntityEventArgs
{
    public readonly bool IsEnabled;

    public OnRadarOverlayToggledEvent(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }
}
