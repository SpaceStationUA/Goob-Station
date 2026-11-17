// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Eui;
using Content.Client.UserInterface.Controls;
using Content.Shared._Pirate.Crayon;
using Content.Shared.Eui;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Pirate.Crayon;

public sealed class CrayonAdminEui : BaseEui
{
    private readonly DefaultWindow _window = new() { MinSize = new Vector2(560, 390) };
    private readonly BoxContainer _body = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly List<OwnedTexture> _textures = new();
    private readonly IClyde _clyde = IoCManager.Resolve<IClyde>();

    public CrayonAdminEui()
    {
        _window.Contents.AddChild(_body);
        _window.OnClose += RequestClose;
    }

    private void RequestClose() => SendMessage(new CloseEuiMessage());

    public override void Opened() => _window.OpenCentered();

    public override void Closed()
    {
        _window.OnClose -= RequestClose;
        _window.Close();
        Clear();
    }

    private void Clear()
    {
        _body.RemoveAllChildren();
        foreach (var texture in _textures)
            texture.Dispose();
        _textures.Clear();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not CrayonAdminEuiState gallery)
            return;

        Clear();
        _window.Title = Loc.GetString("pirate-crayon-admin-title", ("name", gallery.Title));
        if (!gallery.Single)
        {
            var tabs = new BoxContainer();
            _body.AddChild(tabs);
            foreach (var placed in new[] { false, true })
            {
                var tab = new Button
                {
                    Text = Loc.GetString(placed ? "pirate-crayon-admin-placed" : "pirate-crayon-admin-saved"),
                    Disabled = placed == gallery.Placed,
                };
                tab.OnPressed += _ => SendMessage(new CrayonAdminPage(placed, 0));
                tabs.AddChild(tab);
            }

            var refresh = new Button { Text = Loc.GetString("pirate-crayon-admin-refresh") };
            refresh.OnPressed += _ => SendMessage(new CrayonAdminPage(gallery.Placed, gallery.Page));
            tabs.AddChild(refresh);
        }

        var rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        _body.AddChild(new ScrollContainer { VerticalExpand = true, Children = { rows } });
        if (gallery.Rows.Count == 0)
            rows.AddChild(new Label { Text = Loc.GetString("pirate-crayon-admin-empty") });

        foreach (var row in gallery.Rows)
        {
            var preview = new TextureRect { MinSize = new Vector2(80), Stretch = TextureRect.StretchMode.KeepAspectCentered };
            if (row.Symbol != null)
            {
                var texture = CrayonSymbolImage.ToTexture(_clyde, row.Symbol);
                _textures.Add(texture);
                preview.Texture = texture;
            }

            var copy = new Button { Text = Loc.GetString("pirate-crayon-admin-copy"), Disabled = row.Symbol == null };
            copy.OnPressed += _ => SendMessage(new CrayonAdminAction(row.Saved, row.Stamp, false));
            var delete = new ConfirmButton { Text = Loc.GetString("pirate-crayon-custom-delete") };
            delete.OnPressed += _ => SendMessage(new CrayonAdminAction(row.Saved, row.Stamp, true));
            rows.AddChild(new BoxContainer
            {
                Children = { preview, new Label { Text = row.Name, ToolTip = row.Name, ClipText = true, HorizontalExpand = true }, copy, delete },
            });
        }

        if (gallery.Pages > 1)
        {
            var back = new Button { Text = "◀", Disabled = gallery.Page == 0 };
            back.OnPressed += _ => SendMessage(new CrayonAdminPage(gallery.Placed, gallery.Page - 1));
            var forward = new Button { Text = "▶", Disabled = gallery.Page + 1 >= gallery.Pages };
            forward.OnPressed += _ => SendMessage(new CrayonAdminPage(gallery.Placed, gallery.Page + 1));
            _body.AddChild(new BoxContainer { Children = { back, new Label { Text = $"{gallery.Page + 1} / {gallery.Pages}" }, forward } });
        }

        if (gallery.Status != null)
            _body.AddChild(new Label { Text = Loc.GetString(gallery.Status) });
    }
}
