// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Genetics.Mutations;

public sealed class GenomeSnapshot
{
    public readonly List<EntProtoId<MutationComponent>> Dormant = new();

    // Null means the source was not scanned.
    public List<Sequence>? Sequences;
}

public sealed partial class MutationSystem
{
    public void SetDormant(Entity<MutatableComponent?> ent, List<EntProtoId<MutationComponent>> dormant)
    {
        if (_net.IsClient || !_mutatableQuery.Resolve(ent, ref ent.Comp))
            return;

        var delta = 0;
        foreach (var (id, uid) in ent.Comp.Mutations)
        {
            var wasForeign = IsForeign(ent.Comp, id);
            if (wasForeign == !dormant.Contains(id) || !_query.TryComp(uid, out var mutation))
                continue;

            delta += wasForeign ? -mutation.Instability : mutation.Instability;
        }

        ent.Comp.Dormant.Clear();
        ent.Comp.Dormant.AddRange(dormant);
        Dirty(ent, ent.Comp);
        AddInstability((ent.Owner, ent.Comp), delta, automatic: true, predicted: false);
    }
}

public sealed partial class ScannedGenomeSystem
{
    public GenomeSnapshot? GetGenome(EntityUid mob)
    {
        if (!_mutatableQuery.TryComp(mob, out var mutatable))
            return null;

        var genome = new GenomeSnapshot();
        genome.Dormant.AddRange(mutatable.Dormant);
        if (_query.TryComp(mob, out var scanned))
            genome.Sequences = CopySequences(scanned.Sequences);
        return genome;
    }

    public void SetGenome(EntityUid mob, GenomeSnapshot genome)
    {
        if (!_mutatableQuery.HasComp(mob))
            return;

        _mutation.SetDormant(mob, genome.Dormant);
        if (genome.Sequences == null)
        {
            RemComp<ScannedGenomeComponent>(mob);
            return;
        }

        EnsureComp<ScannedGenomeComponent>(mob).Sequences = CopySequences(genome.Sequences);
    }

    private static List<Sequence> CopySequences(List<Sequence> sequences)
        => sequences.Select(s => new Sequence
        {
            Mutation = s.Mutation,
            Bases = s.Bases,
            OriginalBases = s.OriginalBases
        }).ToList();
}
