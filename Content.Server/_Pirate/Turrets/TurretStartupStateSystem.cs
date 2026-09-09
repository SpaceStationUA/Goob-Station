using Content.Server.NPC.HTN;
using Content.Server.Power.EntitySystems;
using Content.Server.Turrets;
using Content.Shared._Pirate.Turrets;
using Content.Shared.Turrets;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server._Pirate.Turrets;

public sealed class TurretStartupStateSystem : EntitySystem
{
    [Dependency] private readonly BatteryWeaponFireModesSystem _fireModes = default!;
    [Dependency] private readonly DeployableTurretSystem _turret = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Deployment requires initialized batteries and HTN.
        SubscribeLocalEvent<TurretStartupStateComponent, MapInitEvent>(OnMapInit,
            after: [typeof(BatterySystem), typeof(HTNSystem)]);
    }

    private void OnMapInit(Entity<TurretStartupStateComponent> ent, ref MapInitEvent args)
    {
        var enabled = ent.Comp.ArmamentState >= 0;

        if (enabled && TryComp<BatteryWeaponFireModesComponent>(ent, out var fireModes))
            _fireModes.TrySetFireMode((ent, fireModes), ent.Comp.ArmamentState);

        if (TryComp<DeployableTurretComponent>(ent, out var turret))
            _turret.TrySetState((ent, turret), enabled);
    }
}
