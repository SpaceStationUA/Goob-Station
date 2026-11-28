// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared.Rejuvenate;
using Content.Shared.Medical;
using Content.Shared.Mobs.Components;

namespace Content.Shared._Pirate.Suspicion.Medical;

public sealed partial class SuspicionReviveSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SuspicionPlayerComponent, TargetDefibrillatedEvent>(OnDefibrillated);
    }

    private void OnDefibrillated(Entity<SuspicionPlayerComponent> ent, ref TargetDefibrillatedEvent args)
    {
        RaiseLocalEvent(ent, new RejuvenateEvent());
    }
}
