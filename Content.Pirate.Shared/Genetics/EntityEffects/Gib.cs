// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.GameStates;

using Content.Shared.EntityEffects;
using Content.Shared.Gibbing.Systems;
using Content.Shared.Gibbing.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Body.Components;

namespace Content.Pirate.Shared.Genetics.EntityEffects;

/// <summary>
/// Gibs the target mob or other (organs bodyparts etc)
/// Whatever it is will be deleted afterwards, be careful.
/// </summary>
public sealed partial class Gib : EntityEffectBase<Gib>
{
    [DataField]
    public bool DropGiblets = true;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-gib", ("chance", Probability));
}

public sealed partial class GibEffectSystem : EntityEffectSystem<TransformComponent, Gib>
{
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private SharedBodySystem _body = default!;

    protected override void Effect(Entity<TransformComponent> ent, ref EntityEffectEvent<Gib> args)
    {
        var dropGiblets = args.Effect.DropGiblets;
        // Pirate: local bodies and detached organs use separate gibbing APIs.
        if (HasComp<BodyComponent>(ent))
            _body.GibBody(ent, gibOrgans: true, gib: dropGiblets ? GibType.Gib : GibType.Skip);
        else if (dropGiblets)
            _gibbing.TryGibEntity(ent.AsNullable(), ent.Owner, GibType.Gib, GibContentsOption.Drop, out _);
        PredictedQueueDel(ent.Owner);
    }
}
