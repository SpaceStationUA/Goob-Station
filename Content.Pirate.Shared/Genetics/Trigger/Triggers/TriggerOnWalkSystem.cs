// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Pirate.Common.Genetics.Movement;
using Content.Shared.Trigger.Systems;

namespace Content.Pirate.Shared.Genetics.Trigger.Triggers;

public sealed partial class TriggerOnWalkSystem : EntitySystem
{
    [Dependency] private TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TriggerOnWalkComponent, FootStepEvent>(OnFootStep);
    }

    private void OnFootStep(Entity<TriggerOnWalkComponent> ent, ref FootStepEvent args)
    {
        _trigger.Trigger(ent, args.Mob, ent.Comp.KeyOut);
    }
}
