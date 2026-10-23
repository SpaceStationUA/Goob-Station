// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Flash;
using Content.Shared.Random.Helpers;
using Content.Shared.Trigger.Systems;
using Content.Pirate.Shared.Genetics.Trigger;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.Genetics.Trigger.Triggers;

public sealed partial class TriggerOnFlashedSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TriggerOnFlashedComponent, AfterFlashedEvent>(OnFlashed);
    }

    private void OnFlashed(Entity<TriggerOnFlashedComponent> ent, ref AfterFlashedEvent args)
    {
        if (SharedRandomExtensions.PredictedProb(_timing, ent.Comp.Prob, GetNetEntity(ent)))
            _trigger.Trigger(ent, args.User, ent.Comp.KeyOut);
    }
}
