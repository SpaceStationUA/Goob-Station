// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.EntityEffects;
using Content.Shared.Random.Helpers;
using Content.Pirate.Shared.Genetics.Mutations;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.Genetics.EntityEffects;

/// <summary>
/// Adds a random negative mutation to the target entity.
/// Does nothing for mutations which are already present or conflict with existing ones.
/// </summary>
public sealed partial class RandomNegativeMutation : EntityEffectBase<RandomNegativeMutation>
{
    /// <summary>
    /// Multiplier on difficulty for sequencing the mutation.
    /// </summary>
    [DataField]
    public float Difficulty = 1f;
}

public sealed partial class RandomNegativeMutationEffectSystem : EntityEffectSystem<MutatableComponent, RandomNegativeMutation>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MutationSystem _mutation = default!;

    protected override void Effect(Entity<MutatableComponent> ent, ref EntityEffectEvent<RandomNegativeMutation> args)
    {
        var rand = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(ent));
        var mutation = rand.Pick(_mutation.NegativeMutations);
        _mutation.AddMutation(ent.AsNullable(), mutation, user: args.User,
            automatic: false, difficulty: args.Effect.Difficulty);
    }
}
