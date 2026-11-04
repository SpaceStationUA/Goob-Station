// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Hands.EntitySystems;

namespace Content.Pirate.Shared.Genetics.Actions;

public sealed partial class ConjureActionSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConjureActionComponent, ConjureActionEvent>(OnAction);
    }

    private void OnAction(Entity<ConjureActionComponent> ent, ref ConjureActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var user = args.Performer;
        var spawned = PredictedSpawnAtPosition(ent.Comp.Spawn, Transform(user).Coordinates);
        _hands.TryPickupAnyHand(user, spawned, animate: false);
        // TODO admin log
    }
}
