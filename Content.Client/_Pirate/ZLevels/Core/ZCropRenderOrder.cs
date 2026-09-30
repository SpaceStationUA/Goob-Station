// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client._Pirate.ZLevels.Core;

internal static class ZCropRenderOrder
{
    /// <summary>Group identical target sizes, retaining first-seen group and within-group order.</summary>
    public static void Fill(IReadOnlyList<ZCropPlanner.Crop> crops, Span<int> order, bool group)
    {
        var count = 0;
        for (var i = 0; i < crops.Count; i++)
        {
            if (order[..count].Contains(i))
                continue;
            order[count++] = i;
            if (!group)
                continue;
            for (var j = i + 1; j < crops.Count; j++)
            {
                if (crops[j].RenderBounds.Size == crops[i].RenderBounds.Size)
                    order[count++] = j;
            }
        }
    }
}
