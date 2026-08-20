// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Goobstation.Wizard.FadingTimedDespawn;
using Content.Shared.Mobs;
using Content.Pirate.Shared.Enchanting;

namespace Content.Server._Pirate.Enchanting;

/// <summary>
/// Starts a vanishing curse only after the mob carrying the cursed item dies.
/// </summary>
public sealed class VanishingCurseSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var query = EntityQueryEnumerator<VanishingCurseComponent, TransformComponent>();
        while (query.MoveNext(out var item, out var curse, out var itemTransform))
        {
            if (HasComp<FadingTimedDespawnComponent>(item) ||
                !_transform.ContainsEntity(args.Target, (item, itemTransform)))
                continue;

            var fading = EnsureComp<FadingTimedDespawnComponent>(item);
            fading.Lifetime = curse.Lifetime;
            fading.FadeOutTime = curse.FadeOutTime;
            Dirty(item, fading);
        }
    }
}
