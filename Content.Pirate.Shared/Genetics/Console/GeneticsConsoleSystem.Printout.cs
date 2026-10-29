// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Pirate.Shared.Genetics.Mutations;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Paper;

namespace Content.Pirate.Shared.Genetics.Console;

public sealed partial class GeneticsConsoleSystem
{
    [Dependency] private LabelSystem _label = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private void InitializePrintout()
    {
        Subs.BuiEvents<GeneticsPrintoutComponent>(GeneticsConsoleUiKey.Key, subs =>
        {
            subs.Event<GeneticsPrintScanMessage>(OnPrintScan);
        });
    }

    private void OnPrintScan(Entity<GeneticsPrintoutComponent> ent, ref GeneticsPrintScanMessage args)
    {
        if (TryPrintHandheldScan(ent, args))
            return;

        if (GetWorkableMob(ent.Owner) is not {} mob ||
            !_genome.IsScanned(mob) ||
            !ResetPrintout(ent))
            return;

        var paper = PredictedSpawnAtPosition(ent.Comp.Paper, Transform(ent).Coordinates);

        var label = string.Empty;
        var text = string.Empty;
        if (args.Index is { } index &&
            _genome.GetSequence(mob, index) is { } sequence &&
            _mutation.GetRoundData(sequence.Mutation) is { } data)
        {
            label = data.Number.ToString();
            text = GetSequenceText(sequence, data);
        }
        else
        {
            label = Identity.Name(mob, EntityManager);
            text = GetScanText(ent, mob, label);
        }
        _paper.SetContent(paper, text);
        _label.Label(paper, label);

        _hands.TryPickupAnyHand(args.Actor, paper);
    }

    private bool ResetPrintout(Entity<GeneticsPrintoutComponent> ent)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextPrint)
            return false;

        ent.Comp.NextPrint = now + ent.Comp.PrintDelay;
        Dirty(ent);
        return true;
    }

    private string GetScanText(EntityUid printer, EntityUid mob, string name)
    {
        _sequences.Clear();
        _genome.AddSequenceStates(mob, _sequences, printer);
        return GetScanText(name, _sequences);
    }

    private string GetScanText(string name, List<SequenceState> sequences)
    {
        _builder.Clear();
        _builder.AppendLine(Loc.GetString("genetics-printout-title"));
        _builder.AppendLine(Loc.GetString("genetics-printout-subject", ("name", name)));
        _builder.AppendLine(Loc.GetString("genetics-printout-sequences", ("count", sequences.Count)));

        foreach (var s in sequences)
        {
            var rarity = s.Rarity.RarityChar();
            _builder.AppendLine(Loc.GetString("genetics-printout-sequence", ("rarity", rarity), ("number", s.Number)));
        }

        return _builder.ToString();
    }

    private string GetSequenceText(Sequence sequence, MutationData data)
    {
        var rarity = _mutation.GetRarity(sequence.Mutation);
        return GetSequenceText(sequence.Bases, data.Number, rarity);
    }

    private string GetSequenceText(string bases, int number, MutationRarity rarity)
    {
        _builder.Clear();
        _builder.AppendLine(Loc.GetString("genetics-printout-title"));
        _builder.AppendLine(Loc.GetString("genetics-printout-sequence-title", ("number", number)));
        _builder.AppendLine(Loc.GetString("genetics-printout-sequence-rarity", ("rarity", rarity)));
        var n = MutationData.PairCount;
        for (int o = 0; o <= n; o += n)
        {
            _builder.Append("| ");
            for (int i = 0; i < n; i += 4)
            {
                var first = o + i;
                var last = first + 4;
                _builder.Append(bases[first..last]);
                _builder.Append(' ');
            }
            _builder.AppendLine("|");
        }
        return _builder.ToString();
    }
}
