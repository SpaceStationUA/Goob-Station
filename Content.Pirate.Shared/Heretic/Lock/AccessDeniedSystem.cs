// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Common.Heretic;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Pirate.Shared.Heretic.Lock;

public sealed class AccessDeniedSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StatusEffectContainerComponent, BeforeAccessReaderCheckEvent>(OnBeforeAccessCheck);
    }

    private void OnBeforeAccessCheck(Entity<StatusEffectContainerComponent> ent, ref BeforeAccessReaderCheckEvent args)
    {
        if (_status.TryEffectsWithComp<AccessDeniedStatusEffectComponent>(ent.Owner, out _))
            args.Cancelled = true;
    }
}
