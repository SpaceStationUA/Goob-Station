// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Knowledge;

/// <summary>
/// Maps a skill level in the inclusive range 0-100 to a gameplay multiplier or offset.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class SkillCurve
{
    [DataField]
    public float SkillScale = 1f;

    [DataField]
    public float SkillOffset;

    [DataField]
    public float CurveScale = 1f;

    [DataField]
    public float CurveOffset;

    public float GetCurve(int skill)
        => GetFinalValue(Math.Clamp(skill, 0, 100) * 0.01f);

    internal float GetFinalValue(float value)
    {
        value = value * SkillScale + SkillOffset;
        return GetValue(value) * CurveScale + CurveOffset;
    }

    internal abstract float GetValue(float value);
}

public sealed partial class LinearSkillCurve : SkillCurve
{
    internal override float GetValue(float value) => value;
}

public sealed partial class RootSkillCurve : SkillCurve
{
    internal override float GetValue(float value) => MathF.Sqrt(Math.Max(value, 0f));
}

public sealed partial class QuadraticSkillCurve : SkillCurve
{
    internal override float GetValue(float value) => value * value;
}

public sealed partial class CubicSkillCurve : SkillCurve
{
    internal override float GetValue(float value) => value * value * value;
}

public sealed partial class SumSkillCurve : SkillCurve
{
    [DataField(required: true)]
    public List<SkillCurve> Curves = new();

    internal override float GetValue(float value)
    {
        var sum = 0f;
        foreach (var curve in Curves)
            sum += curve.GetFinalValue(value);
        return sum;
    }
}
