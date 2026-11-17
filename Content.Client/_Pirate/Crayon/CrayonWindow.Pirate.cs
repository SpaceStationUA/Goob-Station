// SPDX-License-Identifier: AGPL-3.0-only
// Pirate: adapted from AU-14/ColonialMarinesUniverse#2179.

using System.Numerics;
using Content.Client._Pirate.Crayon;
using Content.Client.Stylesheets;
using Content.Shared._Pirate.Crayon;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.Crayon.UI;

public sealed partial class CrayonWindow
{
    [Dependency] private IClyde _pirateClyde = default!;
    [Dependency] private IPlayerManager _piratePlayer = default!;

    private CrayonLibrarySystem Library => _entitySystem.GetEntitySystem<CrayonLibrarySystem>();
    private readonly List<OwnedTexture> _pirateTextures = new();

    // The player's stencil as this window last heard it from the server, and as it stands after
    // what was picked here since. The server has the final say whenever it sends a change.
    private CrayonSymbol? _pirateSeen;
    private CrayonSymbol? _pirateStencil;

    // The saved symbol that draws the stencil, if one does.
    private string? _pirateSelected;

    public event Action<Guid>? OnCustomSelected;

    private void PirateAddCustomSymbols()
    {
        PirateSyncStencil();
        PirateReleaseTextures();
        var grid = new GridContainer { Columns = 6, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var (_, name, symbol) in Library.Drawings)
        {
            var texture = CrayonSymbolImage.ToTexture(_pirateClyde, symbol);
            _pirateTextures.Add(texture);
            var scale = 2f * CrayonSymbol.Small / symbol.Size;
            var button = new TextureButton { TextureNormal = texture, ToolTip = name, Scale = new Vector2(scale, scale) };
            button.OnPressed += _ => PirateSelect(name, symbol);
            grid.AddChild(_pirateSelected == name
                ? new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat { BackgroundColor = StyleNano.ButtonColorDefault },
                    Children = { button },
                }
                : button);
        }

        var create = new Button { Text = Loc.GetString("pirate-crayon-custom-new") };
        create.OnPressed += _ => PirateOpenCanvas(null);
        var edit = new Button { Text = Loc.GetString("pirate-crayon-custom-edit"), Disabled = _pirateSelected == null };
        edit.OnPressed += _ => PirateOpenCanvas(_pirateSelected);
        var delete = new Button { Text = Loc.GetString("pirate-crayon-custom-delete"), Disabled = _pirateSelected == null };
        delete.OnPressed += _ =>
        {
            if (_pirateSelected == null)
                return;

            if (Library.Drawings.Find(d => d.Name == _pirateSelected) is { } selected)
                Library.Delete(selected.Id);
        };

        Grids.AddChild(new Label { Text = Loc.GetString("pirate-crayon-library-count",
            ("count", Library.Drawings.Count), ("max", CrayonDrawing.Limit)) });
        if (Library.Error != null)
            Grids.AddChild(new Label { Text = Loc.GetString(Library.Error) });
        Grids.AddChild(grid);
        Grids.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            Margin = new Thickness(0, 0, 0, 16),
            Children = { create, edit, delete },
        });
    }

    private void PirateSelect(string name, CrayonSymbol symbol)
    {
        if (Library.Drawings.Find(d => d.Name == name) is not { } drawing)
            return;

        _pirateSelected = name;
        _pirateStencil = symbol;
        // A search left in the box would otherwise pick its stock decal again on the next refresh.
        _autoSelected = null;
        OnCustomSelected?.Invoke(drawing.Id);
        RefreshList();
    }

    /// <summary>
    /// A stock decal was picked here, which the server also takes as clearing the stencil.
    /// </summary>
    private void PirateClear()
    {
        _pirateStencil = null;
        _pirateSelected = null;
    }

    /// <summary>
    /// Takes the stencil the server last sent if it changed, from this window or any other crayon's.
    /// </summary>
    private bool PirateSyncStencil()
    {
        CrayonSymbol? stencil = null;
        if (_piratePlayer.LocalEntity is { } player)
            _entitySystem.GetEntitySystem<CrayonStencilSystem>().TryGetStencil(player, out stencil);

        if (ReferenceEquals(stencil, _pirateSeen))
            return false;

        _pirateSeen = stencil;
        _pirateStencil = stencil;
        _pirateSelected = PirateNameOf(stencil);
        return true;
    }

    private string? PirateNameOf(CrayonSymbol? stencil)
    {
        if (stencil == null)
            return null;

        // Two saved symbols can draw the same; keep the one picked.
        if (_pirateSelected != null && Library.Drawings.Find(s => s.Name == _pirateSelected)?.Symbol.SameAs(stencil) == true)
            return _pirateSelected;

        foreach (var (_, name, symbol) in Library.Drawings)
        {
            if (symbol.SameAs(stencil))
                return name;
        }

        return null;
    }

    private void PirateOpenCanvas(string? name)
    {
        var editing = Library.Drawings.Find(d => d.Name == name);
        var canvas = new CrayonCanvasWindow(_color, ColorSelector.Visible, editing?.Id);
        if (editing != null)
            canvas.Load(editing.Name, editing.Symbol);

        canvas.OnSaved += (saved, symbol) =>
        {
            if (IsOpen)
                PirateSelect(saved, symbol);
        };
        canvas.OpenCentered();
    }

    private void OnPirateLibraryChanged()
    {
        _pirateSelected = PirateNameOf(_pirateStencil);
        RefreshList();
    }

    private void PirateReleaseTextures()
    {
        foreach (var texture in _pirateTextures)
        {
            texture.Dispose();
        }

        _pirateTextures.Clear();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        PirateSyncRotation();
        if (PirateSyncStencil())
            RefreshList();
    }

    private void OnPirateWindowClosed()
    {
        Library.Changed -= OnPirateLibraryChanged;
        PirateReleaseTextures();
    }
}
