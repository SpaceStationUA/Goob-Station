// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Buckle;

/// <summary>
/// Applies nested effects to every entity strapped to the target.
/// </summary>
public sealed partial class RelayStrapped : EntityEffectBase<RelayStrapped>
{
    [DataField(required: true)]
    public EntityEffect[] Effects = default!;
}

public sealed class RelayStrappedEffectSystem : EntityEffectSystem<StrapComponent, RelayStrapped>
{
    [Dependency] private readonly SharedEntityEffectsSystem _effects = default!;

    protected override void Effect(Entity<StrapComponent> ent, ref EntityEffectEvent<RelayStrapped> args)
    {
        foreach (var strapped in ent.Comp.BuckledEntities)
        {
            _effects.ApplyEffects(strapped, args.Effect.Effects, args.Scale, args.User);
        }
    }
}

/// <summary>
/// Immediately unbuckles every entity from the target strap.
/// </summary>
public sealed partial class UnbuckleStrapped : EntityEffectBase<UnbuckleStrapped>;

public sealed class UnbuckleStrappedEffectSystem : EntityEffectSystem<StrapComponent, UnbuckleStrapped>
{
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;

    protected override void Effect(Entity<StrapComponent> ent, ref EntityEffectEvent<UnbuckleStrapped> args)
    {
        var buckled = new List<EntityUid>(ent.Comp.BuckledEntities);
        foreach (var target in buckled)
        {
            _buckle.Unbuckle((target, null), args.User);
        }
    }
}

/// <summary>
/// Locks or unlocks a <see cref="StrapLockComponent"/>.
/// </summary>
public sealed partial class StrapLock : EntityEffectBase<StrapLock>
{
    [DataField]
    public bool Unlock;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}

public sealed class StrapLockEffectSystem : EntityEffectSystem<StrapLockComponent, StrapLock>
{
    [Dependency] private readonly StrapLockSystem _strapLock = default!;

    protected override void Effect(Entity<StrapLockComponent> ent, ref EntityEffectEvent<StrapLock> args)
    {
        if (args.Effect.Unlock)
            _strapLock.UnlockStrap(ent);
        else
            _strapLock.LockStrap(ent);
    }
}
