using Content.Shared.Actions;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared._White.Weapons.Ranged.Components;

namespace Content.Shared._White.Xenomorphs.Neurotoxin;

public sealed partial class NeurotoxinSpitActionEvent : WorldTargetActionEvent;

/// <summary>
/// Fires the body's spit gun toward a world click (action icon), instead of RMB.
/// </summary>
public sealed class SharedNeurotoxinSpitSystem : EntitySystem
{
    [Dependency] private readonly SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Raised on the body (raiseOnUser) which has PlasmaAmmoProvider from the gland.
        SubscribeLocalEvent<PlasmaAmmoProviderComponent, NeurotoxinSpitActionEvent>(OnSpit);
    }

    private void OnSpit(EntityUid uid, PlasmaAmmoProviderComponent component, NeurotoxinSpitActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_gun.TryGetActionFireGun(args.Performer, out var bodyGun))
            return;

        // Fire at the clicked point (facing rotates via WorldTargetAction.RotateOnUse).
        args.Handled = _gun.AttemptShoot(args.Performer, bodyGun, args.Target);
    }
}
