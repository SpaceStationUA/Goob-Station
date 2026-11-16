// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Goobstation.UIKit.UserInterface.Humanoid;

public sealed partial class MarkingGridPicker
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        CategoryChips.Measure(new Vector2(availableSize.X, float.PositiveInfinity));

        var rowHeight = 0f;
        foreach (var chip in CategoryChips.Children)
        {
            if (chip.Visible)
                rowHeight = MathF.Max(rowHeight, chip.DesiredSize.Y);
        }

        // Use the current font's measured height so both rows fit at any UI scale.
        var maxHeight = rowHeight > 0 ? rowHeight * 2 + CategoryChips.CrossSeparation : 0;
        if (CategoryScroll.MaxHeight != maxHeight)
            CategoryScroll.MaxHeight = maxHeight;
        return base.MeasureOverride(availableSize);
    }
}
