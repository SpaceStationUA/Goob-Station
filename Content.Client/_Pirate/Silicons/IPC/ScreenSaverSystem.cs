using Content.Shared.Silicons.IPC;
using Robust.Client.GameObjects;
using Content.Shared.Mobs.Systems;

namespace Content.Client.Silicons.IPC;

public sealed class ScreenSaverSystem : EntitySystem
{

    [Dependency] private readonly Robust.Client.UserInterface.IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ScreenSaverActionEvent>(OnAction);
    }

    private void OnAction(ScreenSaverActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_mobState.IsAlive(args.Performer))
            return;

        var uiController = _uiManager.GetUIController<ScreenSaverUIController>();
        uiController.ToggleMenu(args.Performer);
        
        args.Handled = true;
    }
}
