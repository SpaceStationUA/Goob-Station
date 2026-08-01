// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Heretic.Lock;
using Content.Server.Heretic.Abilities;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared.Ghost;
using Content.Shared.Popups;

namespace Content.Pirate.Server.Heretic.Lock;

public sealed class MirrorJauntSystem : EntitySystem
{
    [Dependency] private readonly HereticAbilitySystem _abilities = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EventMirrorJaunt>(OnMirrorJaunt);
    }

    private void OnMirrorJaunt(EventMirrorJaunt args)
    {
        var user = args.Performer;
        if (_lookup.GetEntitiesInRange<ReflectiveSurfaceComponent>(Transform(user).Coordinates, args.LookupRange).Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-ability-fail-mirror-jaunt-no-mirrors"), user, user);
            return;
        }

        if (TryComp(user, out PolymorphedEntityComponent? polymorphed) && HasComp<SpectralComponent>(user))
        {
            _polymorph.Revert((user, polymorphed));
            args.Handled = true;
            return;
        }

        if (!_abilities.TryUseAbility(args))
            return;

        _polymorph.PolymorphEntity(user, args.Polymorph);
    }
}
