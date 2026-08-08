// SPDX-License-Identifier: MIT

using Content.Pirate.Shared.Billiards;
using Robust.Client.UserInterface;

namespace Content.Pirate.Client.Billiards;

public sealed class BilliardTableBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    private BilliardTableWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BilliardTableWindow>();
        _window.OnPyramidPressed += () => SendMessage(new BilliardTableRackMessage(BilliardGameType.Pyramid));
        _window.OnAmericanPoolPressed += () => SendMessage(new BilliardTableRackMessage(BilliardGameType.AmericanPool));
        _window.OnStoragePressed += () => SendMessage(new BilliardTableOpenStorageMessage());

        if (State is BilliardTableBuiState state)
            _window.UpdateState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BilliardTableBuiState cast)
            _window?.UpdateState(cast);
    }
}
