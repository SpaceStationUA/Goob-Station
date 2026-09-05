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

/// <summary>
/// Reproduces the pre-rework Trauma marksmanship spread profile.
/// The returned value is inverse spread because aim-speed consumers divide by skill curves.
/// </summary>
public sealed partial class MarksmanshipSpreadSkillCurve : SkillCurve
{
    [DataField]
    public float UntrainedSpread = 3f;

    [DataField]
    public float BasicTrainingLevel = 26f;

    [DataField]
    public float ExpertLevel = 50f;

    [DataField]
    public float MinimumSpread = 0.05f;

    internal override float GetValue(float value)
    {
        var normalizedLevel = Math.Clamp(value, 0f, 1f);
        var level = normalizedLevel * 100f;
        float spread;

        if (level < BasicTrainingLevel)
        {
            spread = UntrainedSpread
                - level / BasicTrainingLevel
                - normalizedLevel * normalizedLevel;
        }
        else if (level <= ExpertLevel)
        {
            spread = 1f;
        }
        else
        {
            var expertProgress = (level - ExpertLevel) / (100f - ExpertLevel);
            spread = 1f - expertProgress * expertProgress;
        }

        return 1f / MathF.Max(spread, MinimumSpread);
    }
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
