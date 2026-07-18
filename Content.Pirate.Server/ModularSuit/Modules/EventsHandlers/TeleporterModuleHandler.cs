using Content.Shared.Mobs.Components;
using Content.Pirate.Shared.ModularSuit;
using Robust.Shared.Random;

namespace Content.Pirate.Server.ModularSuit;

public sealed partial class TeleporterModuleHandler : ModuleActionHandler
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public const float TeleportRadius = 5f;

    public override void Initialize()
    {
        SubscribeLocalEvent<ModularSuitActionHolderComponent, ActivateTeleporterModuleEvent>(OnActivate);
    }

    private void OnActivate(Entity<ModularSuitActionHolderComponent> ent, ref ActivateTeleporterModuleEvent args)
    {
        if (args.Handled)
            return;

        if (!TryFindModuleByAction(ent, args.Action, out var moduleEnt))
            return;

        if (!TryComp<ModularSuitModuleComponent>(moduleEnt, out var moduleComp) || !moduleComp.IsActive)
            return;

        var attemptEvent = new ModularSuitModuleAttemptEvent(ent.Owner);
        RaiseLocalEvent(moduleEnt.Value, ref attemptEvent);

        if (attemptEvent.Cancelled)
            return;

        if (!TryFindTarget(args.Performer, out var target))
        {
            args.Handled = true;
            return;
        }

        if (!ModularSuit.TryUseCoreCharge(ent.Owner, moduleComp.PowerInstanceUsage))
            return;

        PerformTeleport(args.Performer, target);
        Audio.PlayPvs(args.ActivationSound, args.Performer);
        args.Handled = true;
    }

    private bool TryFindTarget(EntityUid user, out EntityUid target)
    {
        var userCoords = Transform(user).Coordinates;

        var mobs = new HashSet<Entity<MobStateComponent>>();
        _lookup.GetEntitiesInRange(userCoords, TeleportRadius, mobs, LookupFlags.Uncontained);
        mobs.RemoveWhere(mob => mob.Owner == user);

        if (mobs.Count == 0)
        {
            Popup.PopupEntity(Loc.GetString("modsuit-teleporter-no-targets"), user, user);
            target = default;
            return false;
        }

        target = _random.Pick(mobs).Owner;
        return true;
    }

    private void PerformTeleport(EntityUid user, EntityUid target)
    {
        var userCoords = Transform(user).Coordinates;

        _transform.SetCoordinates(user, Transform(target).Coordinates);
        _transform.SetCoordinates(target, userCoords);
    }
}
