// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Shared.Humanoid.Markings;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Humanoid;

public sealed partial class SingleMarkingPicker
{
    /// <summary>Marking set used to resolve gradient icons.</summary>
    public MarkingSet? GradientContext;

    private Texture? GetMarkingTexture(MarkingPrototype marking)
    {
        if (marking.Sprites.Count > 0)
            return _sprite.Frame0(marking.Sprites[0]);

        // Shader-only markings use the parent hair sprite.
        var parentCategory = MarkingCategoriesConversion.FromHumanoidVisualLayers(marking.BodyPart);

        var parent = GradientContext?.Markings.GetValueOrDefault(parentCategory)?.FirstOrDefault();
        if (parent != null
            && _markingManager.TryGetMarking(parent, out var parentProto)
            && parentProto.Sprites.Count > 0)
            return _sprite.Frame0(parentProto.Sprites[0]);

        // Fall back to a category sprite when no parent is selected.
        foreach (var proto in _markingManager.MarkingsByCategory(parentCategory).Values)
            if (proto.Sprites.Count > 0)
                return _sprite.Frame0(proto.Sprites[0]);

        return null;
    }

    /// <summary>Creates gradient parameter sliders; returns false for color layers.</summary>
    private bool TryCreateShaderParamSliders(MarkingPrototype prototype, int colorIndex, Marking marking, BoxContainer container)
    {
        // Shader parameters are read from the marking set.
        var set = new MarkingSet();
        set.AddBack(prototype.MarkingCategory, marking);

        if (_markingManager.GetMarkingShaderParams?.Invoke(prototype, colorIndex, set) is not { } shaderParams)
            return false;

        var sliders = new List<Slider>();
        foreach (var (name, parameter) in shaderParams.OrderBy(x => x.Key))
        {
            var box = new BoxContainer { HorizontalExpand = true };
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
                SliderChanged();
            };
            spinBox.OnValueChanged += args => slider.Value = args.Value;

            box.AddChild(slider);
            box.AddChild(spinBox);
            container.AddChild(new Label { Text = $"{Loc.GetString($"hair-gradient-parameter-{name}")}:" });
            container.AddChild(box);
        }

        return true;

        void SliderChanged()
        {
            var value = Vector4.Zero;
            for (var i = 0; i < sliders.Count && i < 4; i++)
                value[i] = sliders[i].Value;

            marking.SetColor(colorIndex, new Color(value));
            OnColorChanged?.Invoke((_slot, marking));
        }
    }
}
