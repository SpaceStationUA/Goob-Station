// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.EntitySystems;
using Content.Shared._Pirate.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Construction.Components;
using Content.Shared.Examine;
using Content.Shared.NodeContainer;
using Content.Shared.Popups;
using Content.Shared.Power;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server._Pirate.Atmos.EntitySystems;

public sealed class GasFlowMeterSystem : EntitySystem
{
    private const float HeatWarning3 = 700.15f;
    private const float HeatWarning2 = 460.15f;
    private const float HeatWarning1 = 340.15f;
    private const float ColdWarning1 = 270.15f;
    private const float ColdWarning2 = 200.15f;
    private const float ColdWarning3 = 120.15f;

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private float _checkAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasFlowMeterComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<GasFlowMeterComponent, AnchorAttemptEvent>(OnAnchorAttempt);
        SubscribeLocalEvent<GasFlowMeterComponent, PowerChangedEvent>(OnPowerChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _checkAccumulator += frameTime;
        if (_checkAccumulator < 1f)
            return;

        _checkAccumulator = 0f;

        var query = EntityQueryEnumerator<GasFlowMeterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (!xform.Anchored)
            {
                SetOffline(uid);
                continue;
            }

            if (!TryGetAttachedPipe(uid, xform, out var pipe))
            {
                _transform.Unanchor(uid, xform);
                SetOffline(uid);
                continue;
            }

            if (!_power.IsPowered(uid))
            {
                SetOffline(uid);
                continue;
            }

            var pressure = pipe.Air.Pressure;
            var temperature = pipe.Air.Temperature;

            SetVisuals(uid, GetPressureState(pressure), GetTemperatureState(pressure, temperature));
        }
    }

    private void OnPowerChanged(Entity<GasFlowMeterComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
        {
            SetOffline(ent);
            return;
        }

        var xform = Transform(ent);
        if (!xform.Anchored || !TryGetAttachedPipe(ent.Owner, xform, out var pipe))
            return;

        SetVisuals(ent,
            GetPressureState(pipe.Air.Pressure),
            GetTemperatureState(pipe.Air.Pressure, pipe.Air.Temperature));
    }

    private void OnAnchorAttempt(Entity<GasFlowMeterComponent> ent, ref AnchorAttemptEvent args)
    {
        if (TryGetAttachedPipe(ent.Owner, Transform(ent), out _))
            return;

        args.Cancel();
        _popup.PopupClient(Loc.GetString("gas-flow-meter-anchor-no-pipe"), ent.Owner, args.User);
    }

    private void OnExamined(Entity<GasFlowMeterComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var xform = Transform(ent);
        if (!xform.Anchored || !TryGetAttachedPipe(ent.Owner, xform, out var pipe))
        {
            args.PushMarkup(Loc.GetString("gas-flow-meter-examine-connection-error"));
            return;
        }

        if (!_power.IsPowered(ent.Owner))
        {
            args.PushMarkup(Loc.GetString("gas-flow-meter-examine-unpowered"));
            return;
        }

        var pressure = pipe.Air.Pressure.ToString("F2");
        var temperatureKelvin = pipe.Air.Temperature.ToString("F2");
        var temperatureCelsius = (pipe.Air.Temperature - Atmospherics.T0C).ToString("F2");

        args.PushMarkup(Loc.GetString("gas-flow-meter-examine",
            ("pressure", pressure),
            ("temperatureKelvin", temperatureKelvin),
            ("temperatureCelsius", temperatureCelsius)));
    }

    private bool TryGetAttachedPipe(
        EntityUid meter,
        TransformComponent xform,
        [NotNullWhen(true)] out PipeNode? pipe)
    {
        pipe = null;

        if (!TryComp<AtmosPipeLayersComponent>(meter, out var meterLayers) ||
            xform.GridUid is not { } grid ||
            !TryComp<MapGridComponent>(grid, out var gridComp))
        {
            return false;
        }

        var indices = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        foreach (var candidate in _map.GetAnchoredEntities((grid, gridComp), indices))
        {
            if (!HasComp<GasFlowMeterAttachableComponent>(candidate) ||
                !TryComp<AtmosPipeLayersComponent>(candidate, out var candidateLayers) ||
                candidateLayers.CurrentPipeLayer != meterLayers.CurrentPipeLayer ||
                !TryComp<NodeContainerComponent>(candidate, out var nodeContainer))
            {
                continue;
            }

            foreach (var node in nodeContainer.Nodes.Values)
            {
                if (node is not PipeNode pipeNode ||
                    pipeNode.CurrentPipeLayer != meterLayers.CurrentPipeLayer)
                {
                    continue;
                }

                pipe = pipeNode;
                return true;
            }
        }

        return false;
    }

    private void SetOffline(EntityUid uid)
        => SetVisuals(uid, GasFlowMeterPressureState.Offline, GasFlowMeterTemperatureState.Gray);

    private void SetVisuals(EntityUid uid, GasFlowMeterPressureState pressure, GasFlowMeterTemperatureState temperature)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, GasFlowMeterVisuals.PressureState, pressure, appearance);
        _appearance.SetData(uid, GasFlowMeterVisuals.TemperatureState, temperature, appearance);
    }

    private static GasFlowMeterPressureState GetPressureState(float pressure)
    {
        if (pressure <= 0.15f * Atmospherics.OneAtmosphere)
            return GasFlowMeterPressureState.Meter0;

        if (pressure <= 1.8f * Atmospherics.OneAtmosphere)
        {
            var value = Math.Clamp((int) MathF.Round(pressure / (Atmospherics.OneAtmosphere * 0.3f) + 0.5f), 1, 6);
            return (GasFlowMeterPressureState) ((int) GasFlowMeterPressureState.Meter1_1 + value - 1);
        }

        if (pressure <= 30f * Atmospherics.OneAtmosphere)
        {
            var value = Math.Clamp((int) MathF.Round(pressure / (Atmospherics.OneAtmosphere * 5f) - 0.35f) + 1, 1, 6);
            return (GasFlowMeterPressureState) ((int) GasFlowMeterPressureState.Meter2_1 + value - 1);
        }

        if (pressure <= 59f * Atmospherics.OneAtmosphere)
        {
            var value = Math.Clamp((int) MathF.Round(pressure / (Atmospherics.OneAtmosphere * 5f) - 6f) + 1, 1, 6);
            return (GasFlowMeterPressureState) ((int) GasFlowMeterPressureState.Meter3_1 + value - 1);
        }

        return GasFlowMeterPressureState.Meter4;
    }

    private static GasFlowMeterTemperatureState GetTemperatureState(float pressure, float temperature)
    {
        if (pressure == 0f || temperature == 0f)
            return GasFlowMeterTemperatureState.Gray;

        return temperature switch
        {
            >= HeatWarning3 => GasFlowMeterTemperatureState.Red,
            >= HeatWarning2 => GasFlowMeterTemperatureState.Orange,
            >= HeatWarning1 => GasFlowMeterTemperatureState.Yellow,
            >= ColdWarning1 => GasFlowMeterTemperatureState.Lime,
            >= ColdWarning2 => GasFlowMeterTemperatureState.Cyan,
            >= ColdWarning3 => GasFlowMeterTemperatureState.Blue,
            _ => GasFlowMeterTemperatureState.Violet,
        };
    }
}
