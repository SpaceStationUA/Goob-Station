using Content.Pirate.Shared.Vampire.Components;
using Content.Shared.Movement.Systems;

namespace Content.Pirate.Shared.Vampire.Systems;

/// <summary>
/// Applies movement slowdown when a vampire is starving
/// </summary>
public sealed class SharedVampireStarvationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
    }

    private void OnRefreshMovespeed(EntityUid uid, VampireComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.BloodFullness > 0f)
            return;

        args.ModifySpeed(component.StarvationWalkSpeedModifier, component.StarvationSprintSpeedModifier);
    }
}
