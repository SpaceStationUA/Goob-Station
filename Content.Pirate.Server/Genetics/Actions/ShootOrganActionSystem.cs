// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Pirate.Shared.Genetics;
using Content.Shared.Polymorph;
using Content.Server.Polymorph.Systems;
using Content.Pirate.Shared.Genetics.Actions;
using Content.Shared.Popups;
using Content.Shared.Throwing;

namespace Content.Pirate.Server.Genetics.Actions;

public sealed partial class ShootOrganActionSystem : EntitySystem
{
    [Dependency] private GeneticsBodySystem _body = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShootOrganActionComponent, ShootOrganActionEvent>(OnShootOrganAction);
    }

    private void OnShootOrganAction(Entity<ShootOrganActionComponent> ent, ref ShootOrganActionEvent args)
    {
        args.Handled = true;

        var user = args.Performer;
        if (RemoveOrgan(ent, user) is not {} organ)
        {
            _popup.PopupEntity(Loc.GetString("MutationTongueSpike-popup-no-organ", ("organ", ent.Comp.Organ)), user, user);
            return;
        }

        // polymorph isn't predicted so this returns false on client
        if (_polymorph.PolymorphEntity(organ, ent.Comp.Polymorph) is not {} projectile)
            return;

        // used by chemspike
        var projComp = EnsureComp<ActionProjectileComponent>(projectile);
        projComp.Container = args.Action.Comp.Container;
        Dirty(projectile, projComp);

        _throwing.TryThrow(projectile, coordinates: args.Target, user: user,
            predicted: false); // TODO: remove if polymorph gets predicted
    }

    private EntityUid? RemoveOrgan(Entity<ShootOrganActionComponent> ent, EntityUid user)
    {
        if (_body.GetOrgan(user, ent.Comp.Organ) is {} organ &&
            _body.RemoveOrgan(user, organ))
            return organ;

        return null;
    }
}
