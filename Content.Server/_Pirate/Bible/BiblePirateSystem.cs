// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Bible;
using Content.Goobstation.Shared.Religion;
using Content.Shared.Damage;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Bible;

public sealed class BiblePirateSystem : EntitySystem
{
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BibleComponent, EntGotInsertedIntoContainerMessage>(OnInsertedContainer);
    }

    private void OnInsertedContainer(EntityUid uid, BibleComponent component, EntGotInsertedIntoContainerMessage args)
    {
        // If an unholy creature picks up the bible, knock them down
        if (!HasComp<WeakToHolyComponent>(args.Container.Owner))
            return;

        Timer.Spawn(TimeSpan.FromMilliseconds(500), () =>
        {
            _stun.TryParalyze(args.Container.Owner, TimeSpan.FromSeconds(10), true);
            _damageableSystem.TryChangeDamage(args.Container.Owner, component.DamageOnUnholyUse);
            _audio.PlayPvs(component.SizzleSoundPath, args.Container.Owner);
        });
    }
}
