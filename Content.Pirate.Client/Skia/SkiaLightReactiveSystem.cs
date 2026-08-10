// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Skia;
using Robust.Client.GameObjects;

namespace Content.Pirate.Client.Skia;

public sealed class SkiaLightReactiveSystem : SharedSkiaLightReactiveSystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private readonly HashSet<Entity<PointLightComponent>> _lightsInRange = [];
    private readonly HashSet<Entity<SharedPointLightComponent>> _validLightsInRange = [];

    public override HashSet<Entity<SharedPointLightComponent>> GetLights(EntityUid targetEntity)
    {
        _lightsInRange.Clear();
        _lookup.GetEntitiesInRange(Transform(targetEntity).Coordinates, 10f, _lightsInRange);

        _validLightsInRange.Clear();
        foreach (var light in _lightsInRange)
        {
            if (light.Comp.Enabled && !light.Comp.Deleted)
                _validLightsInRange.Add((light.Owner, light.Comp));
        }

        return _validLightsInRange;
    }
}
