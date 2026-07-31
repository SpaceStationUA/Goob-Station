// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Pirate.Shared.Humanoid.HairGradients;
using Content.Shared.Humanoid.Markings;

namespace Content.Pirate.Client.Humanoid.HairGradients;

public sealed class ShaderMarkingManager
{
    [Dependency] private readonly MarkingManager _markingManager = default!;

    public void Initialize()
    {
        _markingManager.GetMarkingShaderParams += GetMarkingShaderParams;
    }

    public void Shutdown()
    {
        _markingManager.GetMarkingShaderParams -= GetMarkingShaderParams;
    }

    private static Dictionary<string, Vector3>? GetMarkingShaderParams(
        MarkingPrototype prototype,
        int colorIndex,
        MarkingSet markingSet)
    {
        if (prototype.Coloring.Layers is not { } layers)
            return null;

        foreach (var layer in layers.Values)
        {
            if (layer.Type is ShaderParamsMarkingColoring coloring && coloring.ColorIndex == colorIndex)
                return coloring.GetParamData(markingSet);
        }

        return null;
    }
}
