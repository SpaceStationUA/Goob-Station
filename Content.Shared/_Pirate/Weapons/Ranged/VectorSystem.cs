// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Projectiles;
using Content.Shared.Timing;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Weapons.Ranged;

public sealed partial class VectorSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UseDelaySystem _delay = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VectorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VectorComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<VectorComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<VectorComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<VectorComponent, GunRefreshModifiersEvent>(OnModifiers,
            after: [typeof(SharedWieldableSystem)]);
        SubscribeLocalEvent<VectorComponent, AmmoShotEvent>(OnShot);
        SubscribeLocalEvent<VectorComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<VectorComponent, ShotAttemptedEvent>(OnAttemptShot);
        SubscribeLocalEvent<VectorComponent, EntInsertedIntoContainerMessage>(OnInsert,
            after: [typeof(SharedGunSystem)]);
    }

    private void OnStartup(Entity<VectorComponent> ent, ref ComponentStartup args) => UpdateStock(ent);

    private void OnState(Entity<VectorComponent> ent, ref AfterAutoHandleStateEvent args) => UpdateStock(ent);

    private void OnVerbs(Entity<VectorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null ||
            !_hands.IsHolding(args.User, ent))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.StockFolded ? "vector-stock-extend" : "vector-stock-fold"),
            Act = () =>
            {
                // The gun may have left the user's hands since the verb was built.
                if (!_hands.IsHolding(user, ent))
                    return;

                ent.Comp.StockFolded = !ent.Comp.StockFolded;
                Dirty(ent);
                UpdateStock(ent);
                _audio.PlayPredicted(ent.Comp.StockFolded ? ent.Comp.FoldSound : ent.Comp.ExtendSound, ent, user);
            },
        });
    }

    private void OnExamine(Entity<VectorComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString(ent.Comp.StockFolded ? "vector-stock-folded" : "vector-stock-extended"));
    }

    private void UpdateStock(Entity<VectorComponent> ent)
    {
        _appearance.SetData(ent, VectorVisuals.StockFolded, ent.Comp.StockFolded);
        _item.SetSize(ent, ent.Comp.StockFolded ? "Normal" : "Large");
        // A collection expression emits CollectionsMarshal.SetCount, which the client sandbox forbids.
        var shape = new List<Box2i> { new(0, 0, ent.Comp.StockFolded ? 1 : 3, 1) };
        _item.SetShape(ent, shape);
        _gun.RefreshModifiers(ent.Owner);
    }

    private void OnModifiers(Entity<VectorComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (ent.Comp.StockFolded)
            return;

        args.AngleIncrease *= 0.5;
        args.MinAngle *= 0.8;
        args.MaxAngle *= 0.8;
        args.CameraRecoilScalar *= 0.5f;
        args.ProjectileSpeed *= ent.Comp.ExtendedSpeedMultiplier;
    }

    private void OnShot(Entity<VectorComponent> ent, ref AmmoShotEvent args)
    {
        // Modify spawned projectiles to preserve cartridge-specific effects.
        var multiplier = ent.Comp.DamageMultiplier + (ent.Comp.StockFolded ? 0f : ent.Comp.ExtendedDamageBonus);
        foreach (var uid in args.FiredProjectiles)
        {
            if (!TryComp<ProjectileComponent>(uid, out var projectile))
                continue;

            projectile.Damage *= multiplier;
            if (!ent.Comp.StockFolded)
                projectile.Damage.ArmorPenetration *= ent.Comp.ExtendedPenetrationMultiplier;
            Dirty(uid, projectile);
        }
    }

    private void OnInsert(Entity<VectorComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState || args.Container.ID != SharedGunSystem.MagazineSlot)
            return;

        _gun.ChamberVectorMagazine(ent, LifeStage(ent) >= EntityLifeStage.MapInitialized);
    }

    private void OnGunShot(Entity<VectorComponent> ent, ref GunShotEvent args)
    {
        if (!TryComp<GunComponent>(ent, out var gun) ||
            gun.SelectedMode != SelectiveFire.SemiAuto || gun.BurstActivated)
            return;

        // Reset only the post-shot delay so pre-shot cooldowns still apply.
        gun.NextFire = _timing.CurTime;
        DirtyField(ent.Owner, gun, nameof(GunComponent.NextFire));
    }

    private void OnAttemptShot(Entity<VectorComponent> ent, ref ShotAttemptedEvent args)
    {
        if (_delay.IsDelayed(ent.Owner))
            args.Cancel();
    }
}
