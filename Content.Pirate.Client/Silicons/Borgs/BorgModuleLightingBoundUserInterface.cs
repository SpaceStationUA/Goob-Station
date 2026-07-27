using Content.Pirate.Shared.Silicons.Borgs;
using Robust.Client.UserInterface;

namespace Content.Pirate.Client.Silicons.Borgs;

public sealed partial class BorgModuleLightingBoundUserInterface : BoundUserInterface
{
    private BorgModuleLightingWindow? _window;

    public BorgModuleLightingBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<BorgModuleLightingWindow>();

        _window.OnUpdate += (color, disco, cycleRate) =>
        {
            SendMessage(new UpdateBorgModuleLightingMessage(color, disco, cycleRate));
            _window.Close();
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is BorgModuleLightingBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
