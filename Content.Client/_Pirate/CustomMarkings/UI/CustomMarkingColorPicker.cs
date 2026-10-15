// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.CustomMarkings.UI;

/// <summary>
/// Inline colour input: current colour chip, a swatch palette, and a Custom button that opens the
/// <see cref="CustomMarkingColorPopup"/>. Mirrors the <see cref="ColorSelectorSliders"/> surface so it can replace it.
/// </summary>
public sealed class CustomMarkingColorPicker : BoxContainer
{
    public const string StyleClassSwatch = "ColorSwatch";

    private static readonly string[] Palette =
    {
        "#1A1A1A", "#4A4A4A", "#8C8C8C", "#C8C8C8", "#F2F2F2", "#2B1B12", "#4E2E1E", "#7A4A2A", "#A0704B", "#C9A27C", "#E8C9A6", "#F6E3C9",
        "#D92B2B", "#F06B1E", "#F2C233", "#59B34A", "#23A08A", "#2E8BE6", "#4B4BE0", "#8A46D6", "#D24BB0", "#E0557A", "#A3E635", "#63E5FF",
    };

    private readonly PanelContainer _chip;
    private readonly Button _custom;
    private CustomMarkingColorPopup? _popup;
    private Color _color = Color.White;

    public Action<Color>? OnColorChanged;

    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            _chip.PanelOverride = new StyleBoxFlat(value);
        }
    }

    /// <summary>Kept for callers written against the engine sliders; the popup always offers every mode.</summary>
    public ColorSelectorSliders.ColorSelectorType SelectorType { get; set; }

    /// <summary>Kept for callers written against the engine sliders; alpha is not editable here.</summary>
    public bool IsAlphaVisible { get; set; }

    public CustomMarkingColorPicker()
    {
        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 6;

        _chip = new PanelContainer { MinSize = new Vector2(24, 24), PanelOverride = new StyleBoxFlat(Color.White), VerticalAlignment = VAlignment.Center };
        AddChild(_chip);

        var swatches = new GridContainer { Columns = 12, HSeparationOverride = 2, VSeparationOverride = 2 };
        foreach (var hex in Palette)
        {
            var color = Color.FromHex(hex);
            var swatch = new ContainerButton
            {
                StyleClasses = { StyleClassSwatch },
                ToolTip = hex,
                Children =
                {
                    new PanelContainer
                    {
                        PanelOverride = new StyleBoxFlat(color),
                        MinSize = new Vector2(20, 20),
                        Margin = new Thickness(2),
                    },
                },
            };
            swatch.OnPressed += _ => Pick(color);
            swatches.AddChild(swatch);
        }
        AddChild(swatches);

        _custom = new Button { Text = Loc.GetString("wf-color-custom"), VerticalAlignment = VAlignment.Center };
        _custom.OnPressed += _ => OpenPopup();
        AddChild(_custom);
    }

    private void Pick(Color color)
    {
        Color = color;
        OnColorChanged?.Invoke(color);
    }

    /// <summary>Opens the popup just below the Custom button, seeded with the current colour.</summary>
    private void OpenPopup()
    {
        if (_popup == null)
        {
            _popup = new CustomMarkingColorPopup();
            _popup.OnColorChanged += Pick;
            UserInterfaceManager.ModalRoot.AddChild(_popup);
        }

        _popup.Color = Color;
        var origin = _custom.GlobalPosition + new Vector2(0, _custom.Height + 2);
        _popup.Open(UIBox2.FromDimensions(origin, new Vector2(260, 334)));
    }

    // Pirate: closing a window removes its controls from the tree without disposing them.
    protected override void ExitedTree()
    {
        base.ExitedTree();
        ClosePopup();
    }

    private void ClosePopup()
    {
        if (_popup is not { } popup)
            return;

        _popup = null;
        popup.OnColorChanged -= Pick;
        popup.Close();
        popup.Orphan();
        popup.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ClosePopup();
        base.Dispose(disposing);
    }
}
