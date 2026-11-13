// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.CriminalRecords;
using Robust.Client.UserInterface;

namespace Content.Client._Pirate.CriminalRecords;

public sealed class CitationPaymentConsoleBoundUserInterface : BoundUserInterface
{
    private CitationPaymentConsoleWindow? _window;

    public CitationPaymentConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        if (IsOpened)
            return;

        base.Open();
        _window = this.CreateWindow<CitationPaymentConsoleWindow>();
        _window.OnPay += (record, citation) => SendMessage(new CitationPaymentMessage(record, citation));
        if (State is CitationPaymentConsoleState state)
            _window.UpdateState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CitationPaymentConsoleState paymentState)
            _window?.UpdateState(paymentState);
    }
}
