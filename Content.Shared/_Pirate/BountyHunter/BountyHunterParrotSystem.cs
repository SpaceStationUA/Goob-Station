using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared._Pirate.BountyHunter;

public sealed class BountyHunterParrotSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BountyHunterParrotComponent, BeforeThrowEvent>(OnBeforeThrow);
        SubscribeLocalEvent<BountyHunterParrotComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<BountyHunterParrotComponent, ShotAttemptedEvent>(OnShootAttempt);
    }

    private void OnShootAttempt(Entity<BountyHunterParrotComponent> ent, ref ShotAttemptedEvent args)
    {
        // Disallow firing guns in all cases.
        args.Cancel();
    }

    private void OnAttackAttempt(EntityUid uid, BountyHunterParrotComponent component, AttackAttemptEvent args)
    {
        // Disallow attacking in all cases.
        args.Cancel();
    }

    private void OnBeforeThrow(Entity<BountyHunterParrotComponent> ent, ref BeforeThrowEvent args)
    {
        // No throwing, either.
        args.Cancelled = true;
    }
}
