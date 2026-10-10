// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Pirate.CustomMarkings;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Client._Pirate.CustomMarkings.UI;

/// <summary>
/// The player's custom markings as tiles on the creator's Markings tab, the same tiles the marking picker under it
/// uses: pressing one puts it on the character being edited or takes it off.
/// </summary>
public sealed partial class CustomMarkingQuickList : BoxContainer
{
    /// <summary>Two rows of tiles; a longer library scrolls.</summary>
    private const float MaxListHeight = 204;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entMan = default!;

    private readonly GridContainer _grid;
    private readonly ScrollContainer _scroll;

    private readonly CustomMarkingSystem _system;
    private List<CustomMarking> _worn = new();
    private Direction _direction = Direction.South;
    private bool _listening;

    /// <summary>Raised with the new list whenever a tile changes what the character wears.</summary>
    public Action<List<CustomMarking>>? OnWornChanged;

    public CustomMarkingQuickList()
    {
        IoCManager.InjectDependencies(this);
        _system = _entMan.System<CustomMarkingSystem>();

        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;

        _grid = new GridContainer { Columns = 6, HSeparationOverride = 4, VSeparationOverride = 4 };
        _scroll = new ScrollContainer
        {
            HScrollEnabled = false,
            ReturnMeasure = true,
            MaxHeight = MaxListHeight,
            HorizontalExpand = true,
            Visible = false,
            Children = { _grid },
        };
        AddChild(_scroll);
    }

    private int MaxWorn => Math.Min(_cfg.GetCVar(CustomMarkingCVars.MaxWorn), CustomMarkingRules.MaxWornCap);

    /// <summary>Shows which markings the character wears. Call when the profile is loaded or changed elsewhere.</summary>
    public void SetWorn(IEnumerable<CustomMarking> worn)
    {
        _worn = worn.ToList();
        Rebuild();
    }

    /// <summary>Turns the tiles to face the way the preview does.</summary>
    public void SetDirection(Direction direction)
    {
        _direction = direction;
        foreach (var child in _grid.Children)
        {
            if (child is CustomMarkingTile tile)
                tile.SetDirection(direction);
        }
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        _system.LibraryUpdated += Rebuild;
        _system.ArtLoaded += OnArtLoaded;
        _listening = true;

        _system.RequestLibrary();
        Rebuild();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        if (!_listening)
            return;

        _system.LibraryUpdated -= Rebuild;
        _system.ArtLoaded -= OnArtLoaded;
        _listening = false;
    }

    protected override void Resized()
    {
        base.Resized();

        var columns = Math.Max(1, (int) (Width / CustomMarkingTile.TileWidth));
        if (_grid.Columns != columns)
            _grid.Columns = columns;
    }

    private void OnArtLoaded(string hash)
    {
        Rebuild();
    }

    private void Rebuild()
    {
        _grid.DisposeAllChildren();

        var library = _system.Library;
        if (library != null)
        {
            foreach (var entry in library)
            {
                AddTile(new CustomMarking(entry.Hash, entry.Placement), entry.Name);
            }
        }

        // Worn markings with no library entry behind them still get a tile, so they can come off.
        foreach (var marking in _worn)
        {
            if (library == null || !library.Any(entry => entry.Hash == marking.Hash && entry.Placement == marking.Placement))
                AddTile(marking, Loc.GetString("wf-custom-marking-library-stray"));
        }

        _scroll.Visible = _grid.ChildCount > 0;
    }

    private void AddTile(CustomMarking marking, string name)
    {
        // The art's RSI can only be named once it is here; until then the tile is blank.
        SpriteSpecifier[]? sprites = null;
        if (_system.TryGetArt(marking.Hash, out _))
            sprites = new SpriteSpecifier[] { new SpriteSpecifier.Rsi(CustomMarkingResources.PathFor(marking.Hash), CustomMarkingResources.State) };

        var worn = _worn.Contains(marking);
        var tile = new CustomMarkingTile(null, name, sprites, _direction)
        {
            Pressed = worn,
            Disabled = !worn && _worn.Count >= MaxWorn,
        };
        tile.ToolTip = Loc.GetString("wf-custom-marking-tile-tooltip",
            ("name", name),
            ("placement", Loc.GetString(CustomMarkingEditorWindow.PlacementName(marking.Placement))));
        tile.OnPressed += _ => Toggle(marking);
        _grid.AddChild(tile);
    }

    private void Toggle(CustomMarking marking)
    {
        if (CustomMarkingRules.Toggle(_worn, marking, MaxWorn))
            OnWornChanged?.Invoke(_worn.ToList());

        Rebuild();
    }
}
