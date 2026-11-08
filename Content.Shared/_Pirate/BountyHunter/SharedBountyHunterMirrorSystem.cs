using Content.Shared._Goobstation.Wizard.MagicMirror;
using Content.Shared._Pirate.Roles.Components;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.UserInterface;
using Robust.Shared.Network;

namespace Content.Shared._Pirate.BountyHunter;

public abstract class SharedBountyHunterMirrorSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BountyHunterMirrorComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<BountyHunterMirrorComponent, BoundUserInterfaceMessageAttempt>(OnMessageAttempt);
    }

    public bool CanUse(EntityUid mirror, EntityUid user)
    {
        if (!TryComp<BountyHunterMirrorComponent>(mirror, out var restricted))
            return true;

        if (restricted.Used || !TryComp<WizardMirrorComponent>(mirror, out var wizard) ||
            (wizard.Target != null && wizard.Target != user))
            return false;

        // The bounty hunter role marker is server-only. The server must check the role.
        return _net.IsClient ||
               _mind.TryGetMind(user, out var mind, out var mindComp) &&
               mindComp.OwnedEntity == user &&
               _roles.MindHasRole<BountyHunterRoleComponent>(mind);
    }

    private void OnOpenAttempt(Entity<BountyHunterMirrorComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!CanUse(ent, args.User))
            args.Cancel();
    }

    private void OnMessageAttempt(Entity<BountyHunterMirrorComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (Equals(args.UiKey, WizardMirrorUiKey.Key) && !CanUse(ent, args.Actor))
            args.Cancel();
    }
}
