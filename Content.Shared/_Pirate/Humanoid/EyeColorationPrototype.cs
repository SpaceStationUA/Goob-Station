using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Humanoid;

/// <summary>
///     Pirate: A prototype containing an eye coloration strategy, used to restrict the eye
///     colors a species can have (e.g. shadowkin cannot have black eyes - black eyes are the
///     mindbroken look). Mirrors <see cref="SkinColorationPrototype"/> for eyes.
/// </summary>
[Prototype("eyeColoration")]
public sealed partial class EyeColorationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The eye coloration strategy specified by this prototype.
    /// </summary>
    [DataField(required: true)]
    public IEyeColorationStrategy Strategy { get; private set; } = default!;
}

/// <summary>
///     Pirate: Takes in a color and returns an allowed eye color.
/// </summary>
public interface IEyeColorationStrategy
{
    /// <summary>
    /// Returns whether or not the provided <see cref="Color" /> is within bounds of this strategy.
    /// </summary>
    bool VerifyEyeColor(Color color);

    /// <summary>
    /// Returns the closest valid eye color to the given <see cref="Color" />.
    /// </summary>
    Color ClosestEyeColor(Color color);

    /// <summary>
    /// Returns the input if it passes <see cref="VerifyEyeColor"/>, otherwise returns <see cref="ClosestEyeColor"/>.
    /// </summary>
    Color EnsureVerified(Color color)
    {
        return VerifyEyeColor(color) ? color : ClosestEyeColor(color);
    }
}

/// <summary>
///     Pirate: Eye coloration strategy that clamps the color within HSV channel ranges.
///     Channel ranges are lists of (min, max) segments, like <c>ClampedHsvMarkingColoration</c>.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class ClampedHsvEyeColoration : IEyeColorationStrategy
{
    /// <summary>Allowed ranges of the hue channel.</summary>
    [DataField]
    public List<(float, float)>? Hue;

    /// <summary>Allowed ranges of the saturation channel.</summary>
    [DataField]
    public List<(float, float)>? Saturation;

    /// <summary>Allowed ranges of the value channel.</summary>
    [DataField]
    public List<(float, float)>? Value;

    /// <summary>
    ///     Color used when the input color is achromatic (pure black/gray, saturation ~0) and
    ///     fails verification. Such colors have no hue to clamp toward, so clamping them would
    ///     just produce another near-black gray - snapping to a visible fallback color keeps
    ///     the result clearly colored (e.g. black eyes become violet).
    /// </summary>
    [DataField]
    public Color? AchromaticFallback;

    public bool VerifyEyeColor(Color color)
    {
        var hsv = Color.ToHsv(color);

        return InRange(hsv.X, Hue)
            && InRange(hsv.Y, Saturation)
            && InRange(hsv.Z, Value);
    }

    public Color ClosestEyeColor(Color color)
    {
        var hsv = Color.ToHsv(color);

        // Achromatic colors have no hue to clamp toward - snap them to the fallback so the
        // result stays visibly colored instead of another shade of near-black gray.
        if (AchromaticFallback is { } fallback && hsv.Y < 0.01f && !VerifyEyeColor(color))
            return fallback;

        hsv.X = ClampChannel(hsv.X, Hue);
        hsv.Y = ClampChannel(hsv.Y, Saturation);
        hsv.Z = ClampChannel(hsv.Z, Value);
        return Color.FromHsv(hsv);
    }

    private static bool InRange(float value, List<(float, float)>? ranges)
    {
        if (ranges == null || ranges.Count == 0)
            return true;

        foreach (var (min, max) in ranges)
        {
            if (value >= min && value <= max)
                return true;
        }

        return false;
    }

    private static float ClampChannel(float value, List<(float, float)>? ranges)
    {
        if (ranges == null || ranges.Count == 0)
            return value;

        foreach (var (min, max) in ranges)
        {
            if (value >= min && value <= max)
                return value;
        }

        var closest = ranges[0].Item1;
        var distance = Math.Abs(value - closest);

        foreach (var (min, max) in ranges)
        {
            var minDistance = Math.Abs(value - min);
            if (minDistance < distance)
            {
                closest = min;
                distance = minDistance;
            }

            var maxDistance = Math.Abs(value - max);
            if (maxDistance < distance)
            {
                closest = max;
                distance = maxDistance;
            }
        }

        return closest;
    }
}
