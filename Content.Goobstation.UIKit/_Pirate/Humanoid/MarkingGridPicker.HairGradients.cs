// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Goobstation.UIKit.UserInterface.Lobby;
using Content.Shared.Humanoid.Markings;
using Robust.Client.UserInterface.Controls;

namespace Content.Goobstation.UIKit.UserInterface.Humanoid;

public sealed partial class MarkingGridPicker
{
    private bool TryCreateShaderParamSliders(MarkingPrototype prototype, int colorIndex)
    {
        if (_markingManager.GetMarkingShaderParams?.Invoke(prototype, colorIndex, _currentMarkings) is not { } parameters)
            return false;

        var sliders = new List<Slider>();
        foreach (var (name, parameter) in parameters.OrderBy(pair => pair.Key))
        {
            var row = new BoxContainer { HorizontalExpand = true, SeparationOverride = 6 };
            var slider = new Slider
            {
                HorizontalExpand = true,
                MinValue = parameter.X,
                MaxValue = parameter.Y,
                Value = parameter.Z,
            };
            var spinBox = new FloatSpinBox(0.01f, 2) { Value = parameter.Z };
            sliders.Add(slider);
            spinBox.IsValid += value => value >= parameter.X && value <= parameter.Y;

            slider.OnValueChanged += args =>
            {
                spinBox.Value = args.Value;
                var value = Vector4.Zero;
                for (var i = 0; i < sliders.Count && i < 4; i++)
                {
                    value[i] = sliders[i].Value;
                }

                SetMarkingColor(prototype.MarkingCategory, prototype.ID, colorIndex, new Color(value));
            };
            spinBox.OnValueChanged += args => slider.Value = args.Value;

            row.AddChild(slider);
            row.AddChild(spinBox);
            ColorEditor.AddChild(new Label
            {
                Text = Loc.GetString($"hair-gradient-parameter-{name}"),
                StyleClasses = { HullStyleClasses.FieldLabel },
            });
            HullStyler.Apply(row);
            ColorEditor.AddChild(row);
        }

        return true;
    }
}
