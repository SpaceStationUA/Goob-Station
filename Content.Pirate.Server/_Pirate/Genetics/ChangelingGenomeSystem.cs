// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Server.Changeling;
using Content.Goobstation.Shared.Changeling.Components;
using Content.Pirate.Shared.Genetics.Mutations;
using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;

namespace Content.Pirate.Server.Genetics;

public sealed class ChangelingGenomeSystem : EntitySystem
{
    [Dependency] private readonly ScannedGenomeSystem _genome = default!;

    private readonly Dictionary<TransformData, GenomeSnapshot> _genomes = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChangelingIdentityComponent, ChangelingDnaStolenEvent>(OnDnaStolen);
        SubscribeLocalEvent<DnaComponent, ChangelingFormAppliedEvent>(OnFormApplied);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _genomes.Clear());
    }

    private void OnDnaStolen(Entity<ChangelingIdentityComponent> ent, ref ChangelingDnaStolenEvent args)
    {
        // Placeholder DNA is shared and cannot identify a genome.
        if (!TryComp<DnaComponent>(args.Victim, out var dna) || dna.DNA != args.Data.DNA)
            return;

        if (_genome.GetGenome(args.Victim) is {} genome)
            _genomes[args.Data] = genome;
    }

    private void OnFormApplied(Entity<DnaComponent> ent, ref ChangelingFormAppliedEvent args)
    {
        // The transform bypasses DNA generation, so refresh blood DNA here.
        Dirty(ent);
        if (ent.Comp.DNA is {} dna)
        {
            var ev = new GenerateDnaEvent { Owner = ent.Owner, DNA = dna };
            RaiseLocalEvent(ent, ref ev);
        }

        if (_genomes.TryGetValue(args.Data, out var genome))
            _genome.SetGenome(ent, genome);
    }
}
