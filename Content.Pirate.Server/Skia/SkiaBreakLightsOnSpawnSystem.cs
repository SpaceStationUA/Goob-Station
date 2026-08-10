// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Skia;

namespace Content.Pirate.Server.Skia;

public sealed class SkiaBreakLightsOnSpawnSystem : EntitySystem
{
    [Dependency] private readonly SkiaScreamSystem _scream = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkiaBreakLightsOnSpawnComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<SkiaBreakLightsOnSpawnComponent> entity, ref MapInitEvent args)
    {
        _scream.ShatterLightsAround(entity.Owner, entity.Comp.Radius, entity.Comp.LineOfSight);
    }
}
