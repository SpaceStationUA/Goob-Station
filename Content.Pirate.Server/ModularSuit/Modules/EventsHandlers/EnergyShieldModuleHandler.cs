using Content.Pirate.Shared.EnergyShield;
using Content.Pirate.Shared.ModularSuit;

namespace Content.Pirate.Server.ModularSuit;

public sealed partial class EnergyShieldModuleHandler : ModuleActionHandler
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ModularSuitActionHolderComponent, ActivateEnergyShieldModuleEvent>(OnToggle);
    }

    private void OnToggle(Entity<ModularSuitActionHolderComponent> ent, ref ActivateEnergyShieldModuleEvent args)
    {
        if (args.Handled)
            return;

        if (!TryFindModuleByAction(ent, args.Action, out var moduleEnt))
            return;

        if (!TryComp<ModularSuitModuleComponent>(moduleEnt, out var moduleComp) || !moduleComp.IsActive)
            return;

        if (!ModularSuit.TryUseCoreCharge(ent.Owner, moduleComp.PowerInstanceUsage))
            return;

        var user = args.Performer;
        var shield = EnsureComp<EnergyShieldOwnerComponent>(user);
        if (shield.ShieldEntity != null)
            QueueDel(shield.ShieldEntity.Value);

        shield.ShieldEntity = Spawn(args.ShieldProto, Transform(user).Coordinates);
        shield.SustainingCount = 5;

        _transform.SetParent(shield.ShieldEntity.Value, user);
        args.Handled = true;
    }
}
