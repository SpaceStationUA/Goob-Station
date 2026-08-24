// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.UserInterface.Controls;
using Content.Shared._Pirate.Forging;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._Pirate.Forging.UI;

public sealed class ForgingAnvilBUI : BoundUserInterface
{
    private static readonly ResPath IngotRsi = new("/Textures/_Pirate/Objects/Specific/forging.rsi");

    private readonly ForgingSystem _forging;
    private readonly SharedMetalSystem _metal;
    private readonly SimpleRadialMenu _metals;
    private readonly SimpleRadialMenu _items;
    private MetalPrototype? _chosenMetal;
    private Color _color = Color.White;

    public ForgingAnvilBUI(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _metals = this.CreateDisposableControl<SimpleRadialMenu>();
        _metals.OnClose += () =>
        {
            if (_chosenMetal is null)
                Close();
        };
        _items = this.CreateWindow<SimpleRadialMenu>();
        _items.Close();
        _forging = EntMan.System<ForgingSystem>();
        _metal = EntMan.System<SharedMetalSystem>();
    }

    protected override void Open()
    {
        base.Open();
        _metals.SetButtons(GetMetalButtons());
        _metals.OpenOverMouseScreenPosition();
    }

    private List<RadialMenuOptionBase> GetMetalButtons()
    {
        var buttons = new List<RadialMenuOptionBase>(_metal.AllMetals.Count);
        foreach (var metal in _metal.AllMetals)
        {
            var icon = new SpriteSpecifier.Rsi(IngotRsi, metal.IngotSprite);
            buttons.Add(new RadialMenuActionOption<MetalPrototype>(OnMetalSelected, metal)
            {
                ToolTip = metal.Name,
                IconSpecifier = RadialMenuIconSpecifier.With(icon),
            });
        }

        return buttons;
    }

    private List<RadialMenuOptionBase> GetItemButtons()
    {
        var buttons = new List<RadialMenuOptionBase>(_forging.AllItems.Count);
        foreach (var (category, items) in _forging.AllItems)
        {
            var nested = GetItemButtons(items);
            if (nested.Count == 0)
                continue;

            buttons.Add(new RadialMenuNestedLayerOption(nested)
            {
                ToolTip = category.Name,
                IconSpecifier = RadialMenuIconSpecifier.With(category.Icon),
                BackgroundColor = _color,
            });
        }

        return buttons;
    }

    private List<RadialMenuOptionBase> GetItemButtons(List<ForgedItemPrototype> items)
    {
        var buttons = new List<RadialMenuOptionBase>(items.Count);
        if (_chosenMetal is not { } metal)
            return buttons;

        foreach (var item in items)
        {
            if (!_forging.CanMakeFrom(item, metal.ID))
                continue;

            buttons.Add(new RadialMenuActionOption<ForgedItemPrototype>(OnItemSelected, item)
            {
                ToolTip = _forging.GetDisplayName(item),
                IconSpecifier = item.Result is { } result
                    ? RadialMenuIconSpecifier.With(result)
                    : RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(item.Sprite!.Value, "icon")),
                BackgroundColor = _color,
            });
        }

        return buttons;
    }

    private void OnMetalSelected(MetalPrototype metal)
    {
        _chosenMetal = metal;
        _color = metal.Color;
        _metals.Close();
        _items.SetButtons(GetItemButtons());
        _items.OpenOverMouseScreenPosition();
    }

    private void OnItemSelected(ForgedItemPrototype item)
    {
        if (_chosenMetal is not { } metal)
            return;

        SendPredictedMessage(new AnvilStartItemMessage(metal.ID, item.ID));
        Close();
    }
}
