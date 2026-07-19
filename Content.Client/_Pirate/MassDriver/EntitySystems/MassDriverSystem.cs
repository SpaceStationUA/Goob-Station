using Content.Shared._Pirate.MassDriver;
using Content.Shared._Pirate.MassDriver.Components;
using Content.Shared._Pirate.MassDriver.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Client._Pirate.MassDriver.EntitySystems;

public sealed partial class MassDriverSystem : SharedMassDriverSystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MassDriverComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, MassDriverComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not MassDriverComponentState state)
            return;

        component.CurrentThrowSpeed = state.CurrentThrowSpeed;
        component.CurrentThrowDistance = state.CurrentThrowDistance;
        component.MaxThrowSpeed = state.MaxThrowSpeed;
        component.MaxThrowDistance = state.MaxThrowDistance;
        component.MinThrowSpeed = state.MinThrowSpeed;
        component.MinThrowDistance = state.MinThrowDistance;
        component.Mode = state.CurrentMassDriverMode;
        component.Hacked = state.Hacked;
        component.Console = GetEntity(state.Console);

        if (component.Console == null)
            return;

        _ui.ClientSendUiMessage(component.Console.Value, MassDriverConsoleUiKey.Key, new MassDriverUpdateUIMessage(state));
    }

    public override void ChangePowerLoad(EntityUid uid, MassDriverComponent component, float powerLoad)
    {
    }
}
