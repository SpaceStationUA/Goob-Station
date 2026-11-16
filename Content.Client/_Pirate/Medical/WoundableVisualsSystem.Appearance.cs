// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Part;
using Robust.Client.GameObjects;

namespace Content.Client._Shitmed.Medical.Surgery.Wounds;

public sealed partial class WoundableVisualsSystem
{
    protected override void OnAppearanceChange(EntityUid uid, WoundableVisualsComponent component, ref AppearanceChangeEvent args)
    {
        // Integrity events can arrive before the wound entities and their containers have finished updating.
        // Appearance changes run after state application, when the current wound data is available.
        var target = uid;
        if (TryComp<BodyPartComponent>(uid, out var part) && part.Body is { } body)
            target = body;

        if (TryComp<SpriteComponent>(target, out var sprite))
            UpdateWoundableVisuals((uid, component), (target, sprite));
    }
}
