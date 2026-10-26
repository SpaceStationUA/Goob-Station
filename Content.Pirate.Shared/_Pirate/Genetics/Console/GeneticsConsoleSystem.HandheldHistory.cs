// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Genetics.Mutations;
using Content.Shared.IdentityManagement;

namespace Content.Pirate.Shared.Genetics.Console;

public sealed partial class GeneticsConsoleSystem
{
    private void InitializeHandheldHistory()
    {
        Subs.BuiEvents<HandheldGeneticsScannerComponent>(GeneticsConsoleUiKey.Key, subs =>
        {
            subs.Event<HandheldGeneticsSelectScanMessage>(OnSelectHandheldScan);
        });
    }

    private void OnSelectHandheldScan(Entity<HandheldGeneticsScannerComponent> ent, ref HandheldGeneticsSelectScanMessage args)
    {
        if (_net.IsClient || !TryComp<GeneticsScannerComponent>(ent, out var scanner) || scanner.Busy ||
            args.Index is {} index && (index < 0 || index >= ent.Comp.Scans.Count))
            return;

        ent.Comp.SelectedScan = args.Index;
        UpdateUI((ent.Owner, scanner));
    }

    private void SaveHandheldScan(Entity<HandheldGeneticsScannerComponent> ent, EntityUid subject)
    {
        var sequences = new List<SequenceState>();
        _genome.AddSequenceStates(subject, sequences, ent);
        var scan = new HandheldGeneticsScan
        {
            Target = subject,
            Name = Identity.Name(subject, EntityManager),
            DnaId = _mutation.GetDna(subject) ?? Loc.GetString("generic-not-available-shorthand"),
            Sequences = sequences
        };
        var index = ent.Comp.Scans.FindIndex(saved => saved.Target == subject);
        if (index < 0)
        {
            index = ent.Comp.Scans.Count;
            ent.Comp.Scans.Add(scan);
        }
        else
            ent.Comp.Scans[index] = scan;
        ent.Comp.SelectedScan = index;
    }

    private HandheldGeneticsHistoryState? UpdateHandheldHistory(Entity<GeneticsScannerComponent> ent, List<SequenceState> sequences)
    {
        if (!TryComp<HandheldGeneticsScannerComponent>(ent, out var handheld))
            return null;

        sequences.Clear();
        if (GetSelectedHandheldScan(handheld) is {} selected)
        {
            sequences.AddRange(selected.Sequences);
        }

        var targets = new List<HandheldGeneticsTarget>();
        foreach (var scan in handheld.Scans)
        {
            var numbers = new List<int>();
            foreach (var sequence in scan.Sequences)
                numbers.Add(sequence.Number);
            targets.Add(new HandheldGeneticsTarget(scan.Name, numbers));
        }
        return new HandheldGeneticsHistoryState(targets, handheld.SelectedScan);
    }

    private static HandheldGeneticsScan? GetSelectedHandheldScan(HandheldGeneticsScannerComponent handheld)
        => handheld.SelectedScan is {} index && index >= 0 && index < handheld.Scans.Count
            ? handheld.Scans[index]
            : null;

    private bool TryPrintHandheldScan(Entity<GeneticsPrintoutComponent> ent, GeneticsPrintScanMessage args)
    {
        if (!TryComp<HandheldGeneticsScannerComponent>(ent, out var handheld))
            return false;

        // Print saved server DNA; do not use the live target.
        if (_net.IsClient)
            return true;

        if (GetSelectedHandheldScan(handheld) is not {} scan)
            return true;

        if (!TryComp<GeneticsScannerComponent>(ent, out var scanner) || scanner.Busy ||
            !_power.IsPowered(ent.Owner) || !ResetPrintout(ent))
            return true;

        var label = scan.Name;
        string text;
        if (args.Index is {} index && index < scan.Sequences.Count)
        {
            var sequence = scan.Sequences[(int) index];
            label = sequence.Number.ToString();
            text = GetSequenceText(sequence.Bases, sequence.Number, sequence.Rarity);
        }
        else
            text = GetScanText(scan.Name, scan.Sequences);

        var paper = PredictedSpawnAtPosition(ent.Comp.Paper, Transform(ent).Coordinates);
        _paper.SetContent(paper, text);
        _label.Label(paper, label);
        _hands.TryPickupAnyHand(args.Actor, paper);
        return true;
    }
}
