using Content.Client._Funkystation.VendingMachines.UI;
using Content.Client.VendingMachines;
using Content.Shared.Access.Systems;
using Content.Shared.VendingMachines;
using Content.Shared._Funkystation.VendingMachines;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using System.Linq;

namespace Content.Client._Funkystation.VendingMachines;

[UsedImplicitly]
public sealed class VendingMachineKeypadBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey), IVendingMachineBoundUi
{
    [ViewVariables]
    private VendingMachineKeypadMenu? _menu;

    [ViewVariables]
    private List<VendingMachineInventoryEntry> _cachedInventory = new();

    [ViewVariables]
    private double _priceMultiplier;
    [ViewVariables]
    private int _credits;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindowCenteredLeft<VendingMachineKeypadMenu>();
        _menu.VendingMachineOwner = Owner;
        _menu.User = IoCManager.Resolve<IPlayerManager>().LocalSession?.AttachedEntity;
        _menu.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _menu.OnCodeEntered += OnCodeEntered;
        _menu.OnAudioPlayed += OnAudioPlayed;
        _menu.OnWithdraw += OnWithdraw;
        Refresh();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not VendingMachineInterfaceState newState)
            return;

        _cachedInventory = newState.Inventory;
        _priceMultiplier = newState.PriceMultiplier;
        _credits = newState.Credits;

        Refresh();
    }

    public void Refresh()
    {
        var enabled = EntMan.TryGetComponent(Owner, out VendingMachineComponent? bendy) && !bendy.Ejecting;

        _menu?.Populate(_cachedInventory, enabled);
        _menu?.SetCredits(_credits, _priceMultiplier);
    }

    public void UpdateAmounts()
    {
        var enabled = EntMan.TryGetComponent(Owner, out VendingMachineComponent? bendy) && !bendy.Ejecting;

        _menu?.UpdateAmounts(_cachedInventory, enabled);
        _menu?.SetCredits(_credits, _priceMultiplier);
    }

    private void OnWithdraw(VendingMachineWithdrawMessage message)
    {
        SendPredictedMessage(new VendingMachineWithdrawMessage());
    }

    private void OnAudioPlayed(VendingMachineKeypadSound type, float pitch)
    {
        SendMessage(new VendingMachineKeypadAudioMessage(type, pitch));
    }

    private VendingMachineCodeResult OnCodeEntered(int slotIndex)
    {
        var selectedItem = _cachedInventory.ElementAtOrDefault(slotIndex);

        if (selectedItem == null)
            return VendingMachineCodeResult.Error;

        var playerManager = IoCManager.Resolve<IPlayerManager>();
        if (playerManager.LocalSession?.AttachedEntity is { } player)
        {
            var accessSystem = EntMan.System<AccessReaderSystem>();
            if (!accessSystem.IsAllowed(player, Owner))
            {
                return VendingMachineCodeResult.NoAccess;
            }
        }

        _menu?.PlayVendAnimation(slotIndex);

        SendPredictedMessage(new VendingMachineEjectMessage(selectedItem.Type, selectedItem.ID));
        return VendingMachineCodeResult.Success;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (_menu == null)
            return;

        _menu.OnCodeEntered -= OnCodeEntered;
        _menu.OnAudioPlayed -= OnAudioPlayed;
        _menu.OnWithdraw -= OnWithdraw;
        _menu.OnClose -= Close;
        _menu.Close();
    }
}
