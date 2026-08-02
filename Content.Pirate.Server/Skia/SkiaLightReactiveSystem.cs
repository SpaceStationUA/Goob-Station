// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Skia;
using Robust.Server.GameObjects;

namespace Content.Pirate.Server.Skia;

public sealed class SkiaLightReactiveSystem : SharedSkiaLightReactiveSystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private EntityQuery<PointLightComponent> _lightQuery;
    private readonly HashSet<Entity<SharedPointLightComponent>> _validLightsInRange = [];

    public override void Initialize()
    {
        base.Initialize();
        _lightQuery = GetEntityQuery<PointLightComponent>();
    }

    public override HashSet<Entity<SharedPointLightComponent>> GetLights(EntityUid targetEntity)
    {
        var entitiesInRange = _lookup.GetEntitiesInRange(targetEntity, 10f);

        _validLightsInRange.Clear();
        foreach (var entity in entitiesInRange)
        {
            if (!_lightQuery.TryComp(entity, out var light) || !light.Enabled && light.NetSyncEnabled || light.Deleted)
                continue;

            _validLightsInRange.Add((entity, light));
        }

        return _validLightsInRange;
    }
}
