// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Pinpointer;
using Content.Server.SurveillanceCamera;
using Content.Shared.SurveillanceCamera;

namespace Content.Server._Pirate.SurveillanceCamera;

public sealed class SurveillanceCameraMonitorNavMapSystem : EntitySystem
{
    [Dependency] private readonly NavMapSystem _navMap = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SurveillanceCameraMonitorComponent, BoundUIOpenedEvent>(OnUiOpen);
    }

    private void OnUiOpen(Entity<SurveillanceCameraMonitorComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is not SurveillanceCameraMonitorUiKey.Key)
            return;

        if (Transform(ent).GridUid is { } grid)
            _navMap.EnsureNavMap(grid);
    }
}
