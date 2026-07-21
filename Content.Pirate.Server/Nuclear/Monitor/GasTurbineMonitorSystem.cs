// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Nuclear.Monitor;
using Content.Pirate.Shared.Nuclear.Turbine;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Nuclear.Monitor;

public sealed partial class GasTurbineMonitorSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private NuclearMonitorSystem _monitor = default!;
    [Dependency] private SharedTurbineSystem _turbine = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    private EntityQuery<TurbineComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        _query = GetEntityQuery<TurbineComponent>();

        SubscribeLocalEvent<GasTurbineMonitorComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<NuclearMonitorComponent, TurbineChangeFlowRateMessage>(_monitor.RelayMessage);
        SubscribeLocalEvent<NuclearMonitorComponent, TurbineChangeStatorLoadMessage>(_monitor.RelayMessage);
    }

    private void OnUiOpened(Entity<GasTurbineMonitorComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!args.UiKey.Equals(TurbineUiKey.Key))
            return;

        _monitor.CheckRange(ent.Owner);
        if (GetTurbine(ent.Owner) is { } turbine)
            _turbine.UpdateUI(turbine, ent.Owner);
        else
            _ui.CloseUi(ent.Owner, TurbineUiKey.Key);
    }

    public Entity<TurbineComponent>? GetTurbine(EntityUid monitor)
        => _monitor.GetLinked(monitor) is { } uid && _query.TryComp(uid, out var comp)
            ? (uid, comp)
            : null;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<GasTurbineMonitorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = _timing.CurTime + comp.UpdateDelay;

            _monitor.CheckRange(uid);
            if (GetTurbine(uid) is { } turbine)
                _turbine.UpdateUI(turbine, uid);
        }
    }
}
