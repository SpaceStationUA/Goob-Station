// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.SurveillanceCamera;
using Content.Server.SurveillanceCamera;
using Content.Shared.Item.ItemToggle;

namespace Content.Pirate.Server.SurveillanceCamera;

public sealed class ToggleableCameraServerSystem : EntitySystem
{
    [Dependency] private ItemToggleSystem _toggle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleableCameraComponent, SurveillanceCameraSetActiveAttemptEvent>(OnSetActiveAttempt);
    }

    private void OnSetActiveAttempt(Entity<ToggleableCameraComponent> ent, ref SurveillanceCameraSetActiveAttemptEvent args)
    {
        if (!_toggle.IsActivated(ent.Owner))
            args.Cancelled = true;
    }
}
