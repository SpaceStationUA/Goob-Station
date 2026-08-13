/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._FarHorizons.Planets.Shields;
using Robust.Client.UserInterface;

namespace Content.Client._FarHorizons.Planets.Shields;

/// <seealso cref="CEShieldGeneratorWindow"/>
public sealed class CEShieldGeneratorBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private CEShieldGeneratorWindow? _window;

    public CEShieldGeneratorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CEShieldGeneratorWindow>();
        _window.SetEntity(Owner);
        _window.OnTogglePressed += enabled => SendMessage(new CEShieldGeneratorToggleMessage { Enabled = enabled });
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CEShieldGeneratorBuiState buiState)
            _window?.UpdateState(buiState);
    }
}
