using Content.Pirate.Shared.PAI;
using Robust.Client.UserInterface;

namespace Content.Pirate.Client.PAI;

public sealed partial class PAIMedInjectorBoundUserInterface : BoundUserInterface
{
    private PAIMedInjectorWindow? _window;

    public PAIMedInjectorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<PAIMedInjectorWindow>();

        _window.OnInject += medId =>
        {
            SendMessage(new PAIMedInjectorInjectMessage(medId));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is PAIMedInjectorBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
