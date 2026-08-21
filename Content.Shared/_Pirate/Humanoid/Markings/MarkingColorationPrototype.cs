using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid.Markings;

[Prototype("markingColoration")]
public sealed partial class MarkingColorationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public MarkingColorationStrategy Strategy { get; private set; } = default!;
}

[ImplicitDataDefinitionForInheritors]
public abstract partial class MarkingColorationStrategy
{
    public abstract Color Clamp(Color color);
}

[DataDefinition]
public sealed partial class ClampedHsvMarkingColoration : MarkingColorationStrategy
{
    [DataField]
    public List<(float, float)>? Hue;

    [DataField]
    public List<(float, float)>? Saturation;

    [DataField]
    public List<(float, float)>? Value;

    public override Color Clamp(Color color)
    {
        var hsv = Color.ToHsv(color);
        hsv.X = ClampChannel(hsv.X, Hue);
        hsv.Y = ClampChannel(hsv.Y, Saturation);
        hsv.Z = ClampChannel(hsv.Z, Value);
        return Color.FromHsv(hsv);
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
