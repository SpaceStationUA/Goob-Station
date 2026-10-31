using Content.Shared._Pirate.CCVars;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client._Pirate.Lobby.UI;

public sealed class CharacterPreviewBackground : PanelContainer
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private static readonly Color DarkGray = Color.FromHex("#40454B");
    private static readonly Color Gray = Color.FromHex("#707780");
    private static readonly Color Light = Color.FromHex("#CBD0D6");
    private const float CheckerSize = 16;

    private string _background = "default";

    public CharacterPreviewBackground()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _cfg.OnValueChanged(PirateVars.CharacterPreviewBackground, OnBackgroundChanged, true);
    }

    protected override void ExitedTree()
    {
        _cfg.UnsubValueChanged(PirateVars.CharacterPreviewBackground, OnBackgroundChanged);
        base.ExitedTree();
    }

    private void OnBackgroundChanged(string background)
    {
        _background = background;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_background is not ("dark-gray" or "gray" or "light" or "checkerboard"))
            return;

        var bounds = GetStyleBox()?.GetContentBox(PixelSizeBox, UIScale) ?? PixelSizeBox;
        var color = _background switch
        {
            "dark-gray" => DarkGray,
            "light" => Light,
            _ => Gray,
        };
        handle.DrawRect(bounds, color);

        if (_background != "checkerboard")
            return;

        var cell = Math.Max(1, MathF.Round(CheckerSize * UIScale));
        var row = 0;
        for (var y = bounds.Top; y < bounds.Bottom; y += cell, row++)
        {
            for (var x = bounds.Left + (row % 2) * cell; x < bounds.Right; x += cell * 2)
            {
                handle.DrawRect(new UIBox2(x, y, Math.Min(x + cell, bounds.Right), Math.Min(y + cell, bounds.Bottom)), Light);
            }
        }
    }
}
