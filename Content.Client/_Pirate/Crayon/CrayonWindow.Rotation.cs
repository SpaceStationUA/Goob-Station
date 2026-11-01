// SPDX-License-Identifier: AGPL-3.0-or-later
// Pirate: OmuStation#1904 rotation state with the precise controls from
// space-wizards/space-station-14#39448 (neomoth, themias), also used by Starlight#1292.

using System.Globalization;
using Content.Shared._Pirate.Crayon;
using Content.Shared.CCVar;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client.Crayon.UI;

public sealed partial class CrayonWindow
{
    [Dependency] private IConfigurationManager _pirateConfig = default!;

    private readonly LineEdit _rotationValue = new() { Text = "0", HorizontalExpand = true, MinWidth = 48 };
    private readonly CheckBox _previewCheck = new();
    private EntityUid? _crayon;
    private Angle? _seenRotation;

    public event Action<float>? OnRotationSelected;

    public void SetCrayon(EntityUid crayon)
    {
        _crayon = crayon;
        PirateSyncRotation();
    }

    private void InitializePirateArt()
    {
        PirateArtControls.AddChild(new Label { Text = Loc.GetString("pirate-crayon-rotation") });
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        // Preserve the source's fine and coarse steps in one row.
        foreach (var step in new[] { -180, -90, -45, -10, -1, 0, 1, 10, 45, 90, 180 })
        {
            if (step == 0)
            {
                row.AddChild(_rotationValue);
                continue;
            }

            var button = new Button
            {
                Text = step.ToString("+0;-0", CultureInfo.InvariantCulture),
                MinWidth = 30,
            };
            button.OnPressed += _ =>
            {
                var degrees = TryReadDegrees(out var value) ? value : (float) (_seenRotation?.Degrees ?? 0);
                OnRotationSelected?.Invoke(degrees + step);
            };
            row.AddChild(button);
        }

        _rotationValue.OnTextEntered += _ =>
        {
            if (TryReadDegrees(out var degrees))
                OnRotationSelected?.Invoke(degrees);
        };
        _rotationValue.ToolTip = Loc.GetString("pirate-crayon-rotation-hint");
        PirateArtControls.AddChild(row);
        _previewCheck.Text = Loc.GetString("pirate-crayon-preview");
        _previewCheck.Pressed = _pirateConfig.GetCVar(CCVars.PirateCrayonPreview);
        _previewCheck.OnToggled += args => _pirateConfig.SetCVar(CCVars.PirateCrayonPreview, args.Pressed);
        PirateArtControls.AddChild(_previewCheck);
    }

    private bool TryReadDegrees(out float degrees)
    {
        return (float.TryParse(_rotationValue.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out degrees)
                || float.TryParse(_rotationValue.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out degrees))
            && float.IsFinite(degrees);
    }

    private void PirateSyncRotation()
    {
        if (_crayon == null || !_piratePlayer.LocalEntity.HasValue)
            return;

        var entities = IoCManager.Resolve<IEntityManager>();
        if (entities.TryGetComponent<CrayonRotationComponent>(_crayon, out var rotation) && rotation.Rotation != _seenRotation)
        {
            _seenRotation = rotation.Rotation;
            _rotationValue.Text = rotation.Rotation.Degrees.ToString("0.##", CultureInfo.CurrentCulture);
        }

        _previewCheck.Pressed = _pirateConfig.GetCVar(CCVars.PirateCrayonPreview);
    }
}
