// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Projectiles;
using Content.Shared.Whitelist;

namespace Content.Pirate.Shared.Skia;

public sealed class SharedSkiaEmbedImmuneSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkiaEmbedImmuneComponent, ProjectileReflectAttemptEvent>(OnProjectileReflectAttempt);
    }

    private void OnProjectileReflectAttempt(Entity<SkiaEmbedImmuneComponent> entity, ref ProjectileReflectAttemptEvent args)
    {
        if (!TryComp<EmbeddableProjectileComponent>(args.ProjUid, out _))
            return;

        if (_whitelist.IsWhitelistFail(entity.Comp.ImmuneTo, args.ProjUid))
            return;

        args.Cancelled = true;
    }
}
