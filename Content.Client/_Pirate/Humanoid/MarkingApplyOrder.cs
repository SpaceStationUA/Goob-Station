// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Humanoid.Markings;

namespace Content.Client.Humanoid;

public static class MarkingApplyOrder
{
    // Layers are inserted above their body part, so head shapes apply last to stay beneath Head overlays.
    public static IEnumerable<List<Marking>> InApplyOrder(this MarkingSet set)
    {
        foreach (var (category, markings) in set.Markings)
        {
            if (category != MarkingCategories.HeadShape)
                yield return markings;
        }

        if (set.Markings.TryGetValue(MarkingCategories.HeadShape, out var shapes))
            yield return shapes;
    }
}
