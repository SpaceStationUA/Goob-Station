using Content.Shared._Pirate.Banking;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Funkystation.Banking.UI;

[UsedImplicitly]
public sealed class AtmKeypadBui : BoundUserInterface
{
    private AtmKeypadWindow? _window;

    public AtmKeypadBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindowCenteredLeft<AtmKeypadWindow>();
        _window.AtmOwner = Owner;
        _window.User = IoCManager.Resolve<Robust.Client.Player.IPlayerManager>().LocalSession?.AttachedEntity;
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.OnWithdrawAttempt += SendMessage;

        if (State != null)
            UpdateState(State);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not ATMBuiState atmState)
            return;

        _window?.UpdateState(atmState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (_window == null)
            return;

        _window.OnWithdrawAttempt -= SendMessage;
        _window.OnClose -= Close;
        _window.Close();
    }
}
