// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Server.Body.Components;
using Content.Pirate.Shared.Genetics.Abilities;
using Content.Pirate.Shared.Genetics.Mutations;

namespace Content.Pirate.Server.Genetics.Abilities;

public sealed partial class ThermalRegulatorMutationSystem : EntitySystem
{
    [Dependency] private EntityQuery<ThermalRegulatorComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThermalRegulatorMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<ThermalRegulatorMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<ThermalRegulatorMutationComponent> ent, ref MutationAddedEvent args)
    {
        if (!_query.TryComp(args.Target, out var comp))
            return;

        comp.ShiveringHeatRegulation *= ent.Comp.Shivering;
        comp.SweatHeatRegulation *= ent.Comp.Sweating;
        comp.MetabolismHeat *= ent.Comp.Metabolism;
        comp.ImplicitHeatRegulation *= ent.Comp.Regulation;
    }

    private void OnRemoved(Entity<ThermalRegulatorMutationComponent> ent, ref MutationRemovedEvent args)
    {
        if (!_query.TryComp(args.Target, out var comp))
            return;

        comp.ShiveringHeatRegulation /= ent.Comp.Shivering;
        comp.SweatHeatRegulation /= ent.Comp.Sweating;
        comp.MetabolismHeat /= ent.Comp.Metabolism;
        comp.ImplicitHeatRegulation /= ent.Comp.Regulation;
    }
}
