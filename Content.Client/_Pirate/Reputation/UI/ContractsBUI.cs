using Content.Shared._Pirate.Reputation;
using Robust.Client.UserInterface;

namespace Content.Client._Pirate.Reputation.UI;

public sealed class ContractsBUI : BoundUserInterface
{
    [ViewVariables]
    private ContractsWindow? _window;

    public ContractsBUI(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ContractsWindow>();
        _window.OnAccept += i => SendMessage(new ContractsAcceptMessage(i));
        _window.OnComplete += i => SendMessage(new ContractsCompleteMessage(i));
        _window.OnReject += i => SendMessage(new ContractsRejectMessage(i));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ContractsState contracts)
            _window?.UpdateState(contracts);
    }
}
