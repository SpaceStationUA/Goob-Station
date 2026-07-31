// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Shared.Humanoid.Markings;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Humanoid;

public sealed partial class MarkingPicker
{
    private bool TryCreateShaderParamSliders(MarkingPrototype prototype, int colorIndex, BoxContainer colorContainer)
    {
        if (_markingManager.GetMarkingShaderParams?.Invoke(prototype, colorIndex, _currentMarkings) is not { } shaderParams)
            return false;

        List<Slider> sliders = new();
        var parameterColorIndex = _currentMarkingColors.Count;

        foreach (var (name, parameter) in shaderParams.OrderBy(x => x.Key))
        {
            var box = new BoxContainer { HorizontalExpand = true };
            var slider = new Slider
            {
                HorizontalExpand = true,
                MinValue = parameter.X,
                MaxValue = parameter.Y,
                Value = parameter.Z
            };
            var spinBox = new FloatSpinBox(0.01f, 2) { Value = parameter.Z };

            sliders.Add(slider);
            spinBox.IsValid += value => value >= parameter.X && value <= parameter.Y;

            slider.OnValueChanged += args =>
            {
                spinBox.Value = args.Value;
                SliderChanged();
            };
            spinBox.OnValueChanged += args => slider.Value = args.Value;

            box.AddChild(slider);
            box.AddChild(spinBox);
            colorContainer.AddChild(new Label { Text = $"{Loc.GetString($"hair-gradient-parameter-{name}")}:" });
            colorContainer.AddChild(box);
        }

        _currentMarkingColors.Add(GetParameterColor());
        return true;

        void SliderChanged()
        {
            _currentMarkingColors[parameterColorIndex] = GetParameterColor();
            ColorChanged(parameterColorIndex);
        }

        Color GetParameterColor()
        {
            var value = Vector4.Zero;
            for (var i = 0; i < sliders.Count && i < 4; i++)
            {
                value[i] = sliders[i].Value;
            }

            return new Color(value);
        }
    }

    private Texture? GetMarkingTexture(MarkingPrototype marking)
    {
        if (marking.Sprites.Count > 0)
            return _sprite.Frame0(marking.Sprites[0]);

        var markings = _currentMarkings.Markings.ToDictionary();
        if (HairMarking != null)
            markings[MarkingCategories.Hair] = new() { HairMarking };
        if (FacialHairMarking != null)
            markings[MarkingCategories.FacialHair] = new() { FacialHairMarking };

        var parent = markings
            .GetValueOrDefault(MarkingCategoriesConversion.FromHumanoidVisualLayers(marking.BodyPart))
            ?.FirstOrDefault();

        if (parent == null || !_markingManager.TryGetMarking(parent, out var parentPrototype))
            return null;

        var sprite = parentPrototype.Sprites.FirstOrDefault();
        return sprite == null ? null : _sprite.Frame0(sprite);
    }
}
