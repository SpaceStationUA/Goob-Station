// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.ListeningPost.DropConsole;
using Robust.Client.UserInterface;

namespace Content.Client._Pirate.ListeningPost.DropConsole;

public sealed class SyndicateDropConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private SyndicateDropConsoleWindow? _window;

    public SyndicateDropConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SyndicateDropConsoleWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

        _window.OnSetMode += manual => SendMessage(new SyndicateDropConsoleSetModeMessage(manual));
        _window.OnSelectTile += (grid, pos) => SendMessage(new SyndicateDropConsoleSelectTileMessage(grid, pos));
        _window.OnNudgeTarget += (grid, delta) => SendMessage(new SyndicateDropConsoleNudgeTargetMessage(grid, delta));
        _window.OnClearTarget += () => SendMessage(new SyndicateDropConsoleClearTargetMessage());
        _window.OnLaunch += () => SendMessage(new SyndicateDropConsoleLaunchMessage());
        _window.OnPodSend += () => SendMessage(new SyndicateDropConsolePodSendMessage());

        if (State is SyndicateDropConsoleUiState state)
            _window.Update(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is SyndicateDropConsoleUiState cast)
            _window?.Update(cast);
    }
}
