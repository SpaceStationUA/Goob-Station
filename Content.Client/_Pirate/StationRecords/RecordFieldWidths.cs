// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.StationRecords;

public sealed class RecordFieldWidths
{
    private const float Extra = 20f;

    private readonly OptionButton[] _dropdowns;
    private readonly Control[] _values;
    private readonly Control[] _extraRows;
    private float _valueWidth;
    private float _labelWidth;

    public RecordFieldWidths(OptionButton[] dropdowns, Control[] values, Control[]? extraRows = null)
    {
        _dropdowns = dropdowns;
        _values = values;
        _extraRows = extraRows ?? [];
    }

    public void Update()
    {
        // Controls outside the UI tree have no stylesheet to measure.
        if (_dropdowns.Length > 0 && !_dropdowns[0].IsInsideTree)
            return;

        var widest = 0f;
        foreach (var dropdown in _dropdowns)
        {
            widest = Math.Max(widest, MeasureWidestOption(dropdown));
        }

        _valueWidth = Math.Max(_valueWidth, widest + Extra);

        foreach (var value in _dropdowns)
        {
            SetValueWidth(value);
        }

        foreach (var value in _values)
        {
            SetValueWidth(value);
        }

        AlignLabels();
    }

    private void SetValueWidth(Control value)
    {
        value.MinWidth = _valueWidth;
        value.MaxWidth = float.PositiveInfinity;
        value.HorizontalExpand = true;
        value.HorizontalAlignment = Control.HAlignment.Stretch;
    }

    private void AlignLabels()
    {
        var labels = new List<Control>();
        foreach (var value in _dropdowns)
        {
            AddLabel(labels, value.Parent);
        }

        foreach (var value in _values)
        {
            AddLabel(labels, value.Parent);
        }

        foreach (var row in _extraRows)
        {
            AddLabel(labels, row);
        }

        foreach (var label in labels)
        {
            label.MinWidth = 0f;
            label.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
            _labelWidth = Math.Max(_labelWidth, label.DesiredSize.X);
        }

        foreach (var label in labels)
        {
            label.MinWidth = _labelWidth;
        }
    }

    private static void AddLabel(List<Control> labels, Control? row)
    {
        if (row is { ChildCount: > 0 })
            labels.Add(row.GetChild(0));
    }

    private static float MeasureWidestOption(OptionButton button)
    {
        if (button.ItemCount == 0)
            return 0f;

        var (minWidth, maxWidth) = (button.MinWidth, button.MaxWidth);
        var selected = button.SelectedId;
        button.MinWidth = 0f;
        button.MaxWidth = float.PositiveInfinity;

        var widest = 0f;
        try
        {
            // Select() updates the label without raising selection events.
            for (var i = 0; i < button.ItemCount; i++)
            {
                button.Select(i);
                button.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
                widest = Math.Max(widest, button.DesiredSize.X);
            }
        }
        finally
        {
            button.TrySelectId(selected);
            button.MinWidth = minWidth;
            button.MaxWidth = maxWidth;
        }

        return widest;
    }
}
