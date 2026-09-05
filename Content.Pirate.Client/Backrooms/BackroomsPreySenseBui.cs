// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Backrooms;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Pirate.Client.Backrooms;

[UsedImplicitly]
public sealed class BackroomsPreySenseBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private BackroomsPreySenseWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<BackroomsPreySenseWindow>();
        _window.TargetSelected += OnTargetSelected;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _window != null)
            _window.TargetSelected -= OnTargetSelected;

        base.Dispose(disposing);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is BackroomsPreySenseBuiState msg)
            _window?.SetTargets(msg.Targets);
    }

    private void OnTargetSelected(BackroomsPreySenseTarget target)
    {
        SendMessage(new BackroomsPreySenseSelectedBuiMsg { Target = target.Target });
        Close();
    }
}
