// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.DeviceLinking.Systems;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.UserInterface;
using Content.Shared.Whitelist;
using Content.Pirate.Shared.Nuclear;
using Content.Pirate.Shared.Nuclear.Monitor;

namespace Content.Pirate.Server.Nuclear.Monitor;

public sealed partial class NuclearMonitorSystem : EntitySystem
{
    [Dependency] private DeviceLinkSystem _device = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    private EntityQuery<DeviceLinkSourceComponent> _sourceQuery = default!;
    private EntityQuery<NuclearMonitorComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sourceQuery = GetEntityQuery<DeviceLinkSourceComponent>();
        _query = GetEntityQuery<NuclearMonitorComponent>();

        SubscribeLocalEvent<NuclearMonitorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NuclearMonitorComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<NuclearMonitorComponent, PortDisconnectedEvent>(OnPortDisconnected);
        SubscribeLocalEvent<NuclearMonitorComponent, AnchorStateChangedEvent>(OnAnchorChanged);
    }

    private void OnMapInit(Entity<NuclearMonitorComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Linked is { } linked && IsLinked(ent, linked))
            return;

        var actualLinked = FindLinkedSource(ent);
        if (ent.Comp.Linked == actualLinked)
            return;

        ent.Comp.Linked = actualLinked;
        Dirty(ent);
    }

    private void OnNewLink(Entity<NuclearMonitorComponent> ent, ref NewLinkEvent args)
    {
        if (args.SinkPort != ent.Comp.LinkingPort || _whitelist.IsWhitelistFail(ent.Comp.Whitelist, args.Source))
            return;

        ent.Comp.Linked = args.Source;
        Dirty(ent);
    }

    private void OnPortDisconnected(Entity<NuclearMonitorComponent> ent, ref PortDisconnectedEvent args)
    {
        if (ent.Comp.Linked != args.RemovedPortUid || args.Port != ent.Comp.LinkingPort)
            return;

        ent.Comp.Linked = FindLinkedSource(ent, args.RemovedPortUid);
        Dirty(ent);

        if (ent.Comp.Linked == null)
        {
            var key = Comp<ActivatableUIComponent>(ent).Key!;
            _ui.CloseUi(ent.Owner, key);
        }
    }

    private void OnAnchorChanged(Entity<NuclearMonitorComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            CheckRange(ent.AsNullable());
    }

    /// <summary>
    /// Get the machine linked to a monitor.
    /// </summary>
    public EntityUid? GetLinked(EntityUid monitor)
    {
        if (!_query.TryComp(monitor, out var comp))
            return null;

        var ent = new Entity<NuclearMonitorComponent>(monitor, comp);
        if (comp.Linked is { } linked && IsLinked(ent, linked))
            return linked;

        var actualLinked = FindLinkedSource(ent);
        if (comp.Linked != actualLinked)
        {
            comp.Linked = actualLinked;
            Dirty(ent);
        }

        return actualLinked;
    }

    private EntityUid? FindLinkedSource(Entity<NuclearMonitorComponent> ent, EntityUid? ignored = null)
    {
        var query = EntityQueryEnumerator<DeviceLinkSourceComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (uid != ignored && IsLinked(ent, uid))
                return uid;
        }

        return null;
    }

    private bool IsLinked(Entity<NuclearMonitorComponent> ent, EntityUid sourceUid)
    {
        if (!_sourceQuery.TryComp(sourceUid, out var source) ||
            _whitelist.IsWhitelistFail(ent.Comp.Whitelist, sourceUid))
        {
            return false;
        }

        foreach (var (_, sinkPort) in _device.GetLinks(sourceUid, ent.Owner, source))
        {
            if (sinkPort == ent.Comp.LinkingPort)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Unlink a monitor if it's outside the linked machine's range.
    /// </summary>
    public void CheckRange(Entity<NuclearMonitorComponent?> ent)
    {
        if (!_query.Resolve(ent, ref ent.Comp) ||
            ent.Comp.Linked is not { } linked ||
            !_sourceQuery.TryComp(linked, out var source) ||
            _transform.InRange(ent.Owner, linked, source.Range))
            return;

        var key = Comp<ActivatableUIComponent>(ent).Key!;
        _ui.CloseUi(ent.Owner, key);
        _device.RemoveSinkFromSource(linked, ent, source);
    }

    /// <summary>
    /// Relay a BUI message to the linked machine.
    /// </summary>
    public void RelayMessage(EntityUid uid, NuclearMonitorComponent comp, NuclearMachineBUIMessage args)
    {
        if (comp.Linked is { } linked && IsLinked((uid, comp), linked))
        {
            args.Monitor = uid;
            RaiseLocalEvent(linked, args);
        }
    }
}
