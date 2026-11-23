using Content.Client._Pirate.Store; // Pirate: viewer-only catalogues.
using Content.Shared.Store;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Client.Store.UI;

[UsedImplicitly]
public sealed class StoreTreeBoundUserInterface(EntityUid owner, Enum uiKey) : StoreCatalogBoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private StoreTreeMenu? _menu;

    private StoreTreeUpdateState? _lastState; // Pirate: reused when prototypes reload.

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<StoreTreeMenu>();
        _menu.OnBuy += id => SendMessage(new StoreBuyListingMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not StoreTreeUpdateState treeState)
            return;

        _lastState = treeState;
        _menu?.UpdateState(treeState);
    }

    public override void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        base.OnProtoReload(args);

        if ((args.WasModified<StoreTreePrototype>() || args.WasModified<ListingPrototype>())
            && _lastState is { } treeState)
            _menu?.UpdateState(treeState, true);
    }
}
