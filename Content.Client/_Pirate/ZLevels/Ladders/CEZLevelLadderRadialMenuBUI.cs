using Content.Client.UserInterface.Controls;
using Content.Shared._White.RadialSelector;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.ZLevels.Ladders;

[UsedImplicitly]
public sealed class CEZLevelLadderRadialMenuBUI : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private SimpleRadialMenu? _menu;
    private RadialSelectorState? _state;

    public CEZLevelLadderRadialMenuBUI(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.OnClose += Close;
        _menu.Track(Owner);
        _menu.OpenOverMouseScreenPosition();

        if (_state != null)
            UpdateMenu(_state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not RadialSelectorState radialState)
            return;

        _state = radialState;
        UpdateMenu(radialState);
    }

    private void UpdateMenu(RadialSelectorState state)
    {
        if (_menu == null)
            return;

        _menu.SetButtons(ConvertEntries(state.Entries));
    }

    private List<RadialMenuOptionBase> ConvertEntries(List<RadialSelectorEntry> entries) // Pirate: multiz - RadialMenuOption renamed to RadialMenuOptionBase upstream
    {
        var result = new List<RadialMenuOptionBase>(entries.Count);

        foreach (var entry in entries)
        {
            if (entry.Prototype == null)
                continue;

            result.Add(new RadialMenuActionOption<string>(OnSelected, entry.Prototype)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(entry.Icon), // Pirate: multiz - Sprite replaced by IconSpecifier upstream
                ToolTip = GetTooltip(entry.Prototype),
            });
        }

        return result;
    }

    private void OnSelected(string selected)
    {
        SendPredictedMessage(new RadialSelectorSelectedMessage(selected));
    }

    private string GetTooltip(string selected)
    {
        if (_prototypeManager.TryIndex<EntityPrototype>(selected, out var proto))
            return Loc.GetString(proto.Name);

        return Loc.GetString(selected);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _menu?.Dispose();
    }
}
