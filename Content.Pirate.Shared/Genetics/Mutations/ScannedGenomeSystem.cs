// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Polymorph;
using Robust.Shared.Random;
using System.Linq;
using System.Text;

namespace Content.Pirate.Shared.Genetics.Mutations;

public sealed partial class ScannedGenomeSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MutationSystem _mutation = default!;
    [Dependency] private EntityQuery<MutatableComponent> _mutatableQuery = default!;
    [Dependency] private EntityQuery<MutationComponent> _mutationQuery = default!;
    [Dependency] private EntityQuery<ScannedGenomeComponent> _query = default!;

    private StringBuilder _builder = new();

    [SubscribeLocalEvent]
    private void OnPolymorphed(Entity<ScannedGenomeComponent> ent, ref PolymorphedEvent args)
    {
        var target = args.NewEntity;
        if (ent.Owner != args.OldEntity || !_mutation.IsMutatable(target))
            return;

        var comp = EnsureComp<ScannedGenomeComponent>(target);
        DebugTools.Assert(comp.Sequences.Count == 0, $"Polymorphed {ToPrettyString(ent)} into a non-empty scanned genome entity {ToPrettyString(target)}, its sequences would be wiped!");
        TransferSequences(ent, (target, comp));
    }

    [SubscribeLocalEvent]
    private void OnMutationAdded(Entity<ScannedGenomeComponent> ent, ref MutationAddedEvent args)
    {
        // A mutation can polymorph its host before the original body's notification is raised.
        if (ent.Owner != args.Target.Owner || args.Automatic || args.Mutation.Comp.Target != ent.Owner)
            return;

        // new mutation added to an already scanned subject, create a sequence for it
        TryAddSequence(ent.AsNullable(), args.Id, args.Mutation.Comp);
    }

    [SubscribeLocalEvent]
    private void OnMutationRemoved(Entity<ScannedGenomeComponent> ent, ref MutationRemovedEvent args)
    {
        // check just incase you are VERY evil and have a mutation that is a mob or something crazy
        // also don't remove for polymorph
        if (ent.Owner != args.Target.Owner || args.Automatic)
            return;

        // don't remove dormant mutations
        if (_mutation.IsForeign(args.Target.Comp, args.Id))
            RemoveSequence(ent, args.Id);
    }

    #region Public API

    /// <summary>
    /// Returns true if a mob had its genome scanned.
    /// </summary>
    public bool IsScanned(EntityUid mob) => _query.HasComp(mob);

    public void ScanGenome(EntityUid mob, EntityUid? device = null)
    {
        var scanned = EnsureComp<ScannedGenomeComponent>(mob);
        var mutatable = _mutatableQuery.Comp(mob);
        var ent = (mob, scanned);
        foreach (var id in mutatable.Dormant)
        {
            TryAddSequence(ent, id, device: device);
        }

        foreach (var (id, uid) in mutatable.Mutations)
        {
            // only add non-dormant so they aren't duplicated
            if (_mutation.IsForeign(mutatable, id))
                TryAddSequence(ent, id, _mutationQuery.CompOrNull(uid), device); // Entity data may use a different difficulty.
        }
    }

    /// <summary>
    /// Gets a sequence at a given index, or null if it doesn't exist.
    /// Does nothing on clients as they have to use BUI state.
    /// </summary>
    public Sequence? GetSequence(EntityUid mob, uint index)
        => _query.TryComp(mob, out var scanned)
            && index < scanned.Sequences.Count
            ? scanned.Sequences[(int) index]
            : null;

    public void TryAddSequence(Entity<ScannedGenomeComponent?> ent, EntProtoId<MutationComponent> id, MutationComponent? mutation = null, EntityUid? device = null)
    {
        if (!_query.Resolve(ent, ref ent.Comp) ||
            _mutation.GetRoundData(id) is not {} data ||
            ent.Comp.Sequences.Any(s => s.Mutation == id)) // no dupes
        {
            return;
        }

        if (mutation == null && !_mutation.AllMutations.TryGetValue(id, out mutation))
            return; // bad id

        if (device is {} scanner && _mutation.IsDiscovered(scanner, id))
        {
            ent.Comp.Sequences.Add(new Sequence
            {
                Mutation = id,
                Bases = data.Bases,
                OriginalBases = data.Bases
            });
            return;
        }

        _builder.Clear();
        _builder.Append(data.Bases);

        // give difficulty a random offset so its a bit harder to metagame what a mutation could be
        // you can still generally go off more bases missing = better but not automatically know
        // exactly what it is by grepping the mutations :)
        var difficulty = mutation.Difficulty;
        difficulty += _random.Next(-2, 2);
        difficulty = Math.Clamp(difficulty, 2, MutationData.BaseCount);

        // chance of Xing out a whole pair goes up with difficulty
        // so you are less likely to get free easy fixes
        var pairChance = (float) difficulty / MutationData.BaseCount;

        // randomly X out bases depending on mutation difficulty
        while (difficulty > 0)
        {
            var pair = _random.Next(0, MutationData.PairCount);
            // The two strands are concatenated, so complements are PairCount bases apart.
            var complement = pair + MutationData.PairCount;
            // cant X out a whole pair if theres only 1 difficulty left
            if (difficulty >= 2 && _random.Prob(pairChance))
            {
                TryX(pair);
                TryX(complement);
            }
            else if (_random.Prob(0.5f))
            {
                TryX(pair);
            }
            else
            {
                TryX(complement);
            }
        }
        var bases = _builder.ToString();
        ent.Comp.Sequences.Add(new Sequence
        {
            Mutation = id,
            Bases = bases,
            OriginalBases = bases
        });

        void TryX(int i)
        {
            if (_builder[i] == 'X')
                return;

            _builder[i] = 'X';
            difficulty--;
        }
    }

    public void AddSequenceStates(EntityUid mob, List<SequenceState> sequences, EntityUid? device = null)
    {
        if (!_query.TryComp(mob, out var scanned))
            return;

        foreach (var sequence in scanned.Sequences)
        {
            var id = sequence.Mutation;
            if (_mutation.GetRoundData(id) is not {} data)
            {
                Log.Error($"Sequence of {ToPrettyString(mob)} contains unknown mutation {id}!");
                continue;
            }

            var rarity = _mutation.GetRarity(id);
            var discovered = device is {} viewer && _mutation.IsDiscovered(viewer, id);
            sequences.Add(new SequenceState(discovered ? data.Bases : sequence.Bases,
                discovered ? data.Bases : sequence.OriginalBases, data.Number, rarity,
                discovered ? id.ToString() : null));
        }
    }

    /// <summary>
    /// Removes a sequence of a specific mutation by swap removing with the last mutation.
    /// </summary>
    public void RemoveSequence(Entity<ScannedGenomeComponent> ent, EntProtoId<MutationComponent> id)
    {
        var sequences = ent.Comp.Sequences;
        var count = sequences.Count;
        if (count == 0)
            return;

        // swap remove, great language
        for (var i = 0; i < count; i++)
        {
            var sequence = sequences[i];
            if (sequence.Mutation != id)
                continue;

            var last = count - 1;
            sequences[i] = sequences[last];
            sequences.RemoveAt(last);
            return;
        }
    }

    /// <summary>
    /// Transfer sequences from a source entity to a target entity.
    /// Clears all sequences on the source entity.
    /// Clears any existing sequences the target may have had.
    /// </summary>
    public void TransferSequences(Entity<ScannedGenomeComponent> ent, Entity<ScannedGenomeComponent> target)
    {
        target.Comp.Sequences.Clear();
        foreach (var sequence in ent.Comp.Sequences)
        {
            target.Comp.Sequences.Add(sequence);
        }
        ent.Comp.Sequences.Clear();
    }

    #endregion
}
