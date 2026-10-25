// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Genetics.Console;

public sealed partial class GeneticsConsoleSystem
{
    private void InitializeHandheld()
    {
        InitializeHandheldHistory();
        SubscribeLocalEvent<HandheldGeneticsScannerComponent, MapInitEvent>(OnHandheldInit);
        SubscribeLocalEvent<HandheldGeneticsScannerComponent, AfterInteractEvent>(OnHandheldInteract);
        SubscribeLocalEvent<HandheldGeneticsScannerComponent, HandheldGeneticsScanDoAfterEvent>(OnHandheldScanDoAfter);
    }

    private void OnHandheldInit(Entity<HandheldGeneticsScannerComponent> ent, ref MapInitEvent args)
    {
        SetScanner(ent.Owner, ent.Owner);
    }

    private void OnHandheldInteract(Entity<HandheldGeneticsScannerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not {} target ||
            !_mutation.CanMutate(target) || !TryComp<GeneticsScannerComponent>(ent, out var scanner) ||
            scanner.Busy || !_power.IsPowered(ent.Owner))
            return;

        args.Handled = true;
        var doAfter = new DoAfterArgs(EntityManager, args.User, scanner.ScanDelay,
            new HandheldGeneticsScanDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true
        };
        Speak(ent, "scanning");
        SetBusy((ent.Owner, scanner), doAfter);
    }

    private void OnHandheldScanDoAfter(Entity<HandheldGeneticsScannerComponent> ent, ref HandheldGeneticsScanDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SetBusy(ent.Owner, false);
        if (_net.IsClient)
            return;

        if (args.Cancelled || args.Target is not {} target || TerminatingOrDeleted(target) ||
            !_mutation.CanMutate(target) || !TryComp<GeneticsScannerComponent>(ent, out var scanner) ||
            !_power.IsPowered(ent.Owner))
        {
            Speak(ent, "scan-failed");
            return;
        }

        if ((_mutation.GetGeneticDamage(target) ?? 0) > scanner.MaxGeneticDamage)
        {
            Speak(ent, "genetic-damage");
            return;
        }

        if (!_genome.IsScanned(target))
            _genome.ScanGenome(target);
        SaveHandheldScan(ent, target);
        UpdateUI(ent.Owner);
        _audio.PlayPvs(scanner.ScanSound, ent.Owner);
        Speak(ent, "scanned");
        _adminLog.Add(LogType.Genetics, LogImpact.Low, $"{target:target} was scanned by {args.User:user} with handheld scanner {ent.Owner:console}");
        _ui.TryOpenUi(ent.Owner, GeneticsConsoleUiKey.Key, args.User);
    }
}

[Serializable, NetSerializable]
public sealed partial class HandheldGeneticsScanDoAfterEvent : SimpleDoAfterEvent;
