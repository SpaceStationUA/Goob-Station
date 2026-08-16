using Content.Pirate.Shared.ModularSuit;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Pirate.Client.ModularSuit.Ui;

public sealed partial class ModularSuitBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan PendingTimeout = TimeSpan.FromSeconds(1);

    [ViewVariables]
    private ModularSuitWindow? _window;

    private readonly Dictionary<NetEntity, (bool Value, TimeSpan Expires)> _pendingModules = new();
    private (bool Value, TimeSpan Expires)? _pendingActive;

    private ModularSuitUiStateSystem _uiState = default!;

    public bool HasPendingToggles => _pendingActive != null || _pendingModules.Count > 0;

    public ModularSuitBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _uiState = EntMan.System<ModularSuitUiStateSystem>();

        _window = this.CreateWindowCenteredLeft<ModularSuitWindow>();
        _window.OnClose += Close;

        _window.OnToggleActive += active =>
        {
            if (EntMan.TryGetComponent<ModularSuitComponent>(Owner, out var suit)
                && _uiState.CanPredictActiveToggle((Owner, suit), active))
            {
                _pendingActive = (active, _timing.CurTime + PendingTimeout);
            }

            SendMessage(new ToggleSuitActiveMessage(active));
            Update();
        };

        _window.OnToggleModule += (moduleUid, active) =>
        {
            if (EntMan.TryGetComponent<ModularSuitComponent>(Owner, out var suit)
                && EntMan.TryGetEntity(moduleUid, out var module)
                && _uiState.CanPredictModuleToggle((Owner, suit), module.Value, active))
            {
                _pendingModules[moduleUid] = (active, _timing.CurTime + PendingTimeout);
            }

            SendMessage(new ToggleModuleMessage(moduleUid, active));
            Update();
        };

        Update();
    }

    public override void Update()
    {
        base.Update();

        if (_window == null || !EntMan.TryGetComponent<ModularSuitComponent>(Owner, out var suit))
            return;

        _window.UpdateState(ApplyPending(_uiState.BuildUiState((Owner, suit))));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        Update();
    }

    private ModularSuitBoundUserInterfaceState ApplyPending(ModularSuitBoundUserInterfaceState state)
    {
        var now = _timing.CurTime;

        foreach (var (moduleUid, pending) in _pendingModules)
        {
            if (now >= pending.Expires)
                _pendingModules.Remove(moduleUid);
        }

        var active = state.Active;
        if (_pendingActive is { } pendingActive)
        {
            if (state.Active == pendingActive.Value || now >= pendingActive.Expires)
                _pendingActive = null;
            else
                active = pendingActive.Value;
        }

        foreach (var module in state.Modules)
        {
            if (!_pendingModules.TryGetValue(module.ModuleUid, out var pending))
                continue;

            if (module.IsActive == pending.Value)
            {
                _pendingModules.Remove(module.ModuleUid);
                continue;
            }

            module.IsActive = pending.Value;
        }

        if (active == state.Active)
            return state;

        return new ModularSuitBoundUserInterfaceState(
            active,
            state.CoreCharge,
            state.MaxCoreCharge,
            state.HasCore,
            state.InfinityCore,
            state.HasBattery,
            state.BatteryCharge,
            state.MaxBatteryCharge,
            state.TotalPowerDraw,
            state.Modules,
            state.Parts,
            state.WearerName);
    }
}
