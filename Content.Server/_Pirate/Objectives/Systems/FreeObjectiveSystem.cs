// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Objectives.Components;
using Content.Shared.Objectives.Components;

namespace Content.Server._Pirate.Objectives.Systems;

public sealed class FreeObjectiveSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FreeObjectiveComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<FreeObjectiveComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = 1f;
    }
}
