// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.EntityEffects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Pirate.Shared.Genetics.EntityEffects;

/// <summary>
/// Multiplies the target entity's fixture radii.
/// Density is unchanged so mass will increase/decrease.
/// </summary>
public sealed partial class ScaleFixtures : EntityEffectBase<ScaleFixtures>
{
    /// <summary>
    /// What to scale fixtures by.
    /// </summary>
    [DataField(required: true)]
    public float Multiplier;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}

public sealed partial class ScaleFixturesEffectSystem : EntityEffectSystem<FixturesComponent, ScaleFixtures>
{
    [Dependency] private SharedPhysicsSystem _physics = default!;

    protected override void Effect(Entity<FixturesComponent> ent, ref EntityEffectEvent<ScaleFixtures> args)
    {
        _physics.ScaleFixtures(ent.AsNullable(), args.Effect.Multiplier);
    }
}
