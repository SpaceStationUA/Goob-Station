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

        // Keep the turret retracted if the requested fire mode cannot be applied.
        if (enabled
            && TryComp<BatteryWeaponFireModesComponent>(ent, out var fireModes)
            && !_fireModes.TrySetFireMode((ent, fireModes), ent.Comp.ArmamentState))
        {
            Log.Error(
                $"{ToPrettyString(ent)} could not be set to armament state {ent.Comp.ArmamentState}; leaving it retracted.");
            enabled = false;
        }

        if (TryComp<DeployableTurretComponent>(ent, out var turret))
            _turret.TrySetState((ent, turret), enabled);
    }
}
