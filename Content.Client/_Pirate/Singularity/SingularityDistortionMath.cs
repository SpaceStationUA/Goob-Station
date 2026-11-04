// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Client._Pirate.Singularity;

internal static class SingularityDistortionMath
{
    public static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    public static bool IsValidSource(Vector2 position, float intensity, float falloffPower)
    {
        return IsFinite(position) && float.IsFinite(intensity) && float.IsFinite(falloffPower) && falloffPower >= 0f;
    }

    public static bool IsValidScale(Vector2 scale)
    {
        return IsFinite(scale) && scale.X > 0f && scale.Y > 0f;
    }

    /// <summary>
    /// Matches singularity.swsl without allowing a distortion to poison cursor coordinates.
    /// </summary>
    public static Vector2 GetOffset(Vector2 delta, Vector2 scale, float intensity, float falloffPower, float maxDistance)
    {
        if (!IsValidSource(delta, intensity, falloffPower) || !IsValidScale(scale) ||
            !float.IsFinite(maxDistance) || maxDistance <= 0f)
            return Vector2.Zero;

        var distance = (delta / scale).Length();
        if (!float.IsFinite(distance) || distance >= maxDistance)
            return Vector2.Zero;

        // Keep the shader and cursor projection identical, including at the exact center.
        var deformation = intensity / MathF.Pow(MathF.Max(distance, 1f), falloffPower);
        deformation *= 1f - MathF.Pow(distance / maxDistance, 4f);
        if (deformation > 0.8f)
            deformation = MathF.Pow(deformation, 0.3f);

        var offset = delta * deformation;
        return IsFinite(offset) ? offset : Vector2.Zero;
    }
}
