// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Client.Atmos.HFR.UI;
using Content.Pirate.Shared.Atmos.HFR;
using Content.Shared.Atmos;
using Robust.Client.UserInterface;

namespace Content.Pirate.Client.Atmos.HFR;

/// <summary>
///     Client-side interface handler for the Hyper-torus Fusion Reactor.
/// </summary>
public sealed class HFRBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private HFRWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<HFRWindow>();

        _window.OnStartPowerToggled += on => SendMessage(new HFRSetPowerMessage(on));
        _window.OnStartCoolingToggled += on => SendMessage(new HFRSetCoolingMessage(on));
        _window.OnStartFuelInjectionToggled += on => SendMessage(new HFRSetFuelSwitchMessage(on));
        _window.OnStartModeratorInjectionToggled += on => SendMessage(new HFRSetModeratorSwitchMessage(on));
        _window.OnRecipeSelected += CycleRecipe;
        _window.OnHeatingConductorChanged += value => SendMessage(new HFRSetHeatingConductorMessage(value));
        _window.OnMagneticConstrictorChanged += value => SendMessage(new HFRSetMagneticConstrictorMessage(value));
        _window.OnFuelInjectionChanged += value => SendMessage(new HFRSetFuelInjectionRateMessage(value));
        _window.OnCurrentDampenerChanged += value => SendMessage(new HFRSetCurrentDampenerMessage(value));
        _window.OnModeratorInjectionChanged += value => SendMessage(new HFRSetModeratorInjectionRateMessage(value));
        _window.OnWasteRemovalToggled += value => SendMessage(new HFRToggleWasteRemovalMessage(value));
        _window.OnModeratorFilteringRateChanged += value => SendMessage(new HFRSetModeratorFilteringRateMessage(value));
        _window.OnModeratorFilterChanged += OnModeratorFilterChanged;
        _window.OnEmergencyShutdown += () => SendMessage(new HFREmergencyShutdownMessage());
    }

    /// <summary>
    ///     Selects a recipe on the server.
    /// </summary>
    private void CycleRecipe(int recipe)
    {
        if (recipe < 0 || recipe >= HfrRecipes.All.Length)
            return;
        SendMessage(new HFRSetRecipeMessage((byte) recipe));
    }

    private void OnModeratorFilterChanged(int gasId)
    {
        SendMessage(new HFRSetModeratorFilterMessage(gasId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window is null || state is not HFRBoundUserInterfaceState cast)
            return;

        _window.SetTemperatures(
            cast.FusionTemperature, cast.ModeratorTemperature, cast.CoolantTemperature, cast.OutputTemperature,
            cast.FusionTempDelta, cast.ModeratorTempDelta, cast.CoolantTempDelta, cast.OutputTempDelta,
            cast.PowerLevel);

        _window.SetGases(cast.FusionGases, cast.ModeratorGases);

        _window.SetStatus(cast.PowerLevel, cast.Integrity, cast.IronContent, cast.HeatOutput, cast.HeatLimiter, cast.Energy,
            cast.MeltdownActive, cast.MeltdownCountdown);
        _window.SetSwitches(cast.StartPower, cast.StartCooling, cast.StartFuel, cast.StartModerator, cast.PowerLevel);
        _window.SetWasteRemoval(cast.WasteRemoval);
        _window.SetTweakables(cast.HeatingConductor, cast.MagneticConstrictor, cast.FuelInjectionRate, cast.ModeratorInjectionRate, cast.CurrentDampener, cast.ModeratorFilteringRate);
        _window.SetFilterSelection(cast.ModeratorFilterId);
        _window.SetRecipe(cast.Recipe);
        _window.SetOutputStatus(cast.OutputConnected
            ? "Output port: connected"
            : "Output port: not connected");
    }
}
