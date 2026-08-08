// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Humanoid.Markings;

public static class GradientMarkingCategories
{
    public static bool IgnoresMatchSkin(this MarkingCategories category)
    {
        return category is MarkingCategories.HairSpecial or MarkingCategories.FacialHairSpecial;
    }
}
