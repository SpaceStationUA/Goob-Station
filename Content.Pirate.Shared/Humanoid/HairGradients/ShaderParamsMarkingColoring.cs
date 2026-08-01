// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Shared.Humanoid.Markings;

namespace Content.Pirate.Shared.Humanoid.HairGradients;

public sealed partial class ShaderParamsMarkingColoring : MarkingColoring
{
    [DataField(required: true)]
    public Dictionary<string, Vector2> Params = new();

    public Dictionary<string, Vector3>? GetParamData(MarkingSet markingSet)
    {
        if (GetRgba(markingSet) is not { } color)
            return null;

        var result = new Dictionary<string, Vector3>();
        foreach (var parameter in Params.OrderBy(x => x.Key))
        {
            result[parameter.Key] = new Vector3(parameter.Value, color[result.Count]);
        }

        return result;
    }

    public override Color? GetCleanColor(Color? skin, Color? eyes, MarkingSet markingSet)
    {
        if (GetRgba(markingSet) is not { } color)
            return null;

        var result = Vector4.Zero;
        var ranges = Params.OrderBy(x => x.Key).Select(x => x.Value).ToList();
        for (var i = 0; i < ranges.Count; i++)
        {
            result[i] = Math.Clamp(color[i], ranges[i].X, ranges[i].Y);
        }

        return new Color(result);
    }

    private Vector4? GetRgba(MarkingSet markingSet)
    {
        if (Params.Count is 0 or > 4)
            return null;

        return markingSet.Markings.GetValueOrDefault(MarkingCategory)
            ?.FirstOrDefault(x => x.MarkingId == MarkingId)
            ?.MarkingColors.ElementAtOrDefault(ColorIndex)
            .RGBA;
    }
}
