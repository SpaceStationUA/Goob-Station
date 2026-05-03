using Content.Shared._Pirate.Banking.Components;
using Content.Server.Station.Systems;
using Content.Shared.Paper;

namespace Content.Server._Pirate.Banking;

public sealed class CommandBudgetSystem : EntitySystem
{
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CommandBudgetPinPaperComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, CommandBudgetPinPaperComponent component, MapInitEvent args)
    {
        if (!TryComp(_station.GetOwningStation(uid), out StationBankComponent? account))
            return;

        var pin = account.BankAccount.AccountPin;
        _paper.SetContent(uid, Loc.GetString("command-budget-pin-message", ("pin", pin)));
    }
}
