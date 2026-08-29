// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Access.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceNetwork;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Access.Systems;

/// <summary>
/// Uses one timer per scanner and a bounded spatial lookup. It never enumerates every ID card.
/// </summary>
public sealed class AccessScannerSystem : EntitySystem
{
    public static readonly TimeSpan PoweredScanInterval = TimeSpan.FromSeconds(0.2);
    public static readonly TimeSpan IdleScanInterval = TimeSpan.FromSeconds(1);

    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDeviceLinkSystem _device = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _nearby = [];
    private EntityQuery<AccessScannerBlacklistComponent> _blacklistQuery;
    private EntityQuery<IdCardComponent> _idQuery;

    public override void Initialize()
    {
        base.Initialize();
        _blacklistQuery = GetEntityQuery<AccessScannerBlacklistComponent>();
        _idQuery = GetEntityQuery<IdCardComponent>();

        SubscribeLocalEvent<AccessScannerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AccessScannerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AccessScannerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<AccessScannerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<AccessScannerComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnStartup(Entity<AccessScannerComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Settings.Count == 0)
        {
            Log.Error($"Access scanner {ToPrettyString(ent)} has no settings and was disabled.");
            return;
        }

        ent.Comp.Setting = Math.Clamp(ent.Comp.Setting, 0, ent.Comp.Settings.Count - 1);
        _device.EnsureSourcePorts(ent.Owner, ent.Comp.ActivePort, ent.Comp.NamePort, ent.Comp.JobPort);
        _power.SetLoad(ent.Owner, ent.Comp.Settings[ent.Comp.Setting].Power);

        if (!_net.IsServer)
            return;

        var generation = ++ent.Comp.ScanGeneration;
        ScheduleScan(ent.Owner, generation, TimeSpan.Zero);
    }

    private void OnShutdown(Entity<AccessScannerComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.ScanGeneration++;
        ent.Comp.Scanned.Clear();
        if (ent.Comp.Active)
            _device.SendSignal(ent.Owner, ent.Comp.ActivePort, false);
        ent.Comp.Active = false;
    }

    private void ScheduleScan(EntityUid uid, int generation, TimeSpan delay)
    {
        Timer.Spawn(delay, () => RunScheduledScan(uid, generation));
    }

    private void RunScheduledScan(EntityUid uid, int generation)
    {
        if (!TryComp<AccessScannerComponent>(uid, out var scanner) || scanner.ScanGeneration != generation)
            return;

        var powered = _power.IsPowered(uid);
        if (TryComp<AccessReaderComponent>(uid, out var reader))
            ScanNearby((uid, scanner, reader), powered);

        ScheduleScan(uid, generation, powered ? PoweredScanInterval : IdleScanInterval);
    }

    /// <summary>
    /// Performs one bounded scan. Exposed to make the spatial and access behavior directly testable.
    /// </summary>
    public void ScanNearby(Entity<AccessScannerComponent, AccessReaderComponent> ent, bool powered)
    {
        if (!powered || ent.Comp1.Settings.Count == 0)
        {
            ent.Comp1.Scanned.Clear();
            UpdateActive((ent.Owner, ent.Comp1), false);
            return;
        }

        var index = Math.Clamp(ent.Comp1.Setting, 0, ent.Comp1.Settings.Count - 1);
        var range = ent.Comp1.Settings[index].Range;
        if (range <= 0f)
        {
            ent.Comp1.Scanned.Clear();
            UpdateActive((ent.Owner, ent.Comp1), false);
            return;
        }

        _nearby.Clear();
        var coordinates = _transform.GetMapCoordinates(ent.Owner);
        // LookupFlags.All stays spatially bounded, then recursively includes descendants in nearby containers.
        _lookup.GetEntitiesInRange(coordinates.MapId, coordinates.Position, range, _nearby, LookupFlags.All);

        ent.Comp1.Scanned.RemoveWhere(uid =>
            TerminatingOrDeleted(uid) ||
            !_nearby.Contains(uid) ||
            !_idQuery.HasComp(uid) ||
            _blacklistQuery.HasComp(uid) ||
            !_access.IsAllowed(uid, ent.Owner, ent.Comp2));

        foreach (var uid in _nearby)
        {
            if (!_idQuery.TryComp(uid, out var id) ||
                _blacklistQuery.HasComp(uid) ||
                !_access.IsAllowed(uid, ent.Owner, ent.Comp2) ||
                !ent.Comp1.Scanned.Add(uid))
            {
                continue;
            }

            if (id.FullName is { } name)
                SendString(ent.Owner, ent.Comp1.NamePort, name);
            if (id.LocalizedJobTitle is { } job)
                SendString(ent.Owner, ent.Comp1.JobPort, job);
        }

        UpdateActive((ent.Owner, ent.Comp1), ent.Comp1.Scanned.Count > 0);
    }

    private void OnExamined(Entity<AccessScannerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.Settings.Count == 0)
            return;

        var index = Math.Clamp(ent.Comp.Setting, 0, ent.Comp.Settings.Count - 1);
        args.PushMarkup($"Радіус сканування: [bold]{ent.Comp.Settings[index].Range} м[/bold].");
    }

    private void OnInteractUsing(Entity<AccessScannerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Settings.Count == 0 || !_tool.HasQuality(args.Used, ent.Comp.SettingTool))
            return;

        args.Handled = true;
        ent.Comp.Setting = (ent.Comp.Setting + 1) % ent.Comp.Settings.Count;
        Dirty(ent);

        var setting = ent.Comp.Settings[ent.Comp.Setting];
        _power.SetLoad(ent.Owner, setting.Power);
        _popup.PopupEntity($"Радіус сканера встановлено на {setting.Range} м.", ent.Owner, args.User);
        _audio.PlayPredicted(ent.Comp.CycleSound, ent.Owner, args.User);
    }

    private void OnPowerChanged(Entity<AccessScannerComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
        {
            ent.Comp.Scanned.Clear();
            UpdateActive(ent, false);
            return;
        }

        if (_net.IsServer && TryComp<AccessReaderComponent>(ent.Owner, out var reader))
            ScanNearby((ent.Owner, ent.Comp, reader), true);
    }

    private void SendString(EntityUid uid, string port, string value)
    {
        var data = new NetworkPayload
        {
            ["logic_string"] = value,
        };
        _device.InvokePort(uid, port, data);
    }

    private void UpdateActive(Entity<AccessScannerComponent> ent, bool active)
    {
        if (ent.Comp.Active == active)
            return;

        ent.Comp.Active = active;
        _device.SendSignal(ent.Owner, ent.Comp.ActivePort, active);
    }
}
