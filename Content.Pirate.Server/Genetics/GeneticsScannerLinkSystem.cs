// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Medical.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Pirate.Common.Genetics.Medical;
using Content.Pirate.Shared.Genetics.Console;
using Robust.Shared.Containers;

namespace Content.Pirate.Server.Genetics;

/// <summary>
/// Pirate: connects Trauma genetics to the existing medical scanner without changing cloning.
/// </summary>
public sealed partial class GeneticsScannerLinkSystem : EntitySystem
{
    private const string Port = "MedicalScannerSender";

    [SubscribeLocalEvent(after: [typeof(DeviceLinkSystem)])]
    private void OnMapInit(Entity<GeneticsScannerComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<DeviceLinkSourceComponent>(ent, out var source))
            return;
        foreach (var scanner in source.Outputs.Values.SelectMany(value => value))
        {
            if (HasComp<MedicalScannerComponent>(scanner))
            {
                Connect(ent, scanner);
                break;
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnLink(Entity<GeneticsScannerComponent> ent, ref NewLinkEvent args)
    {
        if (args.SourcePort == Port && HasComp<MedicalScannerComponent>(args.Sink))
            Connect(ent, args.Sink);
    }

    [SubscribeLocalEvent]
    private void OnDisconnect(Entity<GeneticsScannerComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port == Port && ent.Comp.Scanner is {} scanner)
            Disconnect(ent, scanner);
    }

    [SubscribeLocalEvent]
    private void OnInserted(Entity<MedicalScannerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container != ent.Comp.BodyContainer)
            return;
        var query = EntityQueryEnumerator<GeneticsScannerComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (console.Scanner != ent.Owner)
                continue;
            var ev = new ScannerInsertedEvent(ent, args.Entity);
            RaiseLocalEvent(uid, ref ev);
        }
    }

    [SubscribeLocalEvent]
    private void OnRemoved(Entity<MedicalScannerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container != ent.Comp.BodyContainer)
            return;
        var query = EntityQueryEnumerator<GeneticsScannerComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (console.Scanner != ent.Owner)
                continue;
            var ev = new ScannerEjectedEvent(ent, args.Entity);
            RaiseLocalEvent(uid, ref ev);
        }
    }

    [SubscribeLocalEvent]
    private void OnScannerShutdown(Entity<MedicalScannerComponent> ent, ref ComponentShutdown args)
    {
        var query = EntityQueryEnumerator<GeneticsScannerComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (console.Scanner == ent.Owner)
                Disconnect((uid, console), ent);
        }
    }

    public void Connect(Entity<GeneticsScannerComponent> console, EntityUid scanner)
    {
        if (!TryComp<MedicalScannerComponent>(scanner, out var comp))
            return;
        if (console.Comp.Scanner is {} old)
            Disconnect(console, old);
        var connected = new ScannerConnectedEvent(scanner);
        RaiseLocalEvent(console, ref connected);
        if (comp.BodyContainer.ContainedEntity is {} mob)
        {
            var inserted = new ScannerInsertedEvent(scanner, mob);
            RaiseLocalEvent(console, ref inserted);
        }
    }

    private void Disconnect(Entity<GeneticsScannerComponent> console, EntityUid scanner)
    {
        if (console.Comp.ScannedMob is {} mob)
        {
            var ejected = new ScannerEjectedEvent(scanner, mob);
            RaiseLocalEvent(console, ref ejected);
        }
        var disconnected = new ScannerDisconnectedEvent(scanner);
        RaiseLocalEvent(console, ref disconnected);
    }
}
