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

    [Dependency] private readonly IEntityManager _pirateEntities = default!;

    private CrayonLibrarySystem Library => _entitySystem.GetEntitySystem<CrayonLibrarySystem>();
    private readonly List<OwnedTexture> _pirateTextures = new();

    // The player's stencil as this window last heard it from the server, and as it stands after
    // what was picked here since. The server has the final say whenever it sends a change.
    private CrayonSymbol? _pirateSeen;
    private CrayonSymbol? _pirateStencil;

    // The saved symbol that draws the stencil, if one does.
    private Guid? _pirateSeenId;
    private Guid? _pirateSelected;

    public event Action<Guid>? OnCustomSelected;

    private void PirateAddCustomSymbols()
    {
        PirateSyncStencil();
        PirateReleaseTextures();
        var grid = new GridContainer { Columns = 6, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var drawing in Library.Drawings)
        {
            var (id, name, symbol) = drawing;
            var texture = CrayonSymbolImage.ToTexture(_pirateClyde, symbol);
            _pirateTextures.Add(texture);
            var scale = 2f * CrayonSymbol.Small / symbol.Size;
            var button = new TextureButton { TextureNormal = texture, ToolTip = name, Scale = new Vector2(scale, scale) };
            button.OnPressed += _ => PirateSelect(drawing);
            grid.AddChild(_pirateSelected == id
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
        edit.OnPressed += _ =>
        {
            if (Library.Drawings.Find(d => d.Id == _pirateSelected) is { } drawing)
                PirateOpenCanvas(drawing);
        };
        var delete = new Button { Text = Loc.GetString("pirate-crayon-custom-delete"), Disabled = _pirateSelected == null };
        delete.OnPressed += _ =>
        {
            if (_pirateSelected is { } selected)
                Library.Delete(selected);
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

    private void PirateSelect(CrayonDrawing drawing)
    {
        _pirateSelected = drawing.Id;
        _pirateStencil = drawing.Symbol;
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
        CrayonStencilComponent? stencil = null;
        if (_piratePlayer.LocalEntity is { } player)
            _pirateEntities.TryGetComponent(player, out stencil);

        if (ReferenceEquals(stencil?.Symbol, _pirateSeen) && stencil?.DrawingId == _pirateSeenId)
            return false;

        _pirateSeen = _pirateStencil = stencil?.Symbol;
        _pirateSeenId = _pirateSelected = stencil?.DrawingId;
        return true;
    }

    private void PirateOpenCanvas(CrayonDrawing? editing)
    {
        var canvas = new CrayonCanvasWindow(_color, ColorSelector.Visible, editing?.Id);
        if (editing != null)
            canvas.Load(editing.Name, editing.Symbol);

        canvas.OnSaved += id =>
        {
            if (IsOpen && Library.Drawings.Find(d => d.Id == id) is { } saved)
                PirateSelect(saved);
        };
        canvas.OpenCentered();
    }

    private void OnPirateLibraryChanged()
    {
        if (_pirateSelected != null && !Library.Drawings.Exists(d => d.Id == _pirateSelected))
            _pirateSelected = null;
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

    protected override void EnteredTree()
    {
        base.EnteredTree();
        Library.Changed += OnPirateLibraryChanged;
        OnPirateLibraryChanged();
    }

    protected override void ExitedTree()
    {
        Library.Changed -= OnPirateLibraryChanged;
        PirateReleaseTextures();
        base.ExitedTree();
    }
}
