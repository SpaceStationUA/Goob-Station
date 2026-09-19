using Content.Pirate.Shared.ModularSuit;
using Content.Shared.Emp;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.ModularSuit;

public sealed partial class ModularSuitEmpShieldModuleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ModularSuitSystem _modularSuit = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModularSuitEmpShieldModuleComponent, ModularSuitInstalledEvent>(OnInstalled);
        SubscribeLocalEvent<ModularSuitEmpShieldModuleComponent, ModularSuitRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<ModularSuitEmpShieldModuleComponent, ModularSuitModuleToggledEvent>(OnToggled);
        SubscribeLocalEvent<ModularSuitEmpShieldModuleComponent, ComponentShutdown>(OnShutdown);

        // Metadata exists on every entity, so nested contents are covered.
        SubscribeLocalEvent<MetaDataComponent, EmpAttemptEvent>(OnEmpAttempt);
    }

    private void OnInstalled(Entity<ModularSuitEmpShieldModuleComponent> module, ref ModularSuitInstalledEvent args)
    {
        if (TryComp<ModularSuitModuleComponent>(module.Owner, out var moduleComp) && moduleComp.IsActive)
            Enable(module, args.Suit, GetSuitWearer(args.Suit));
    }

    private void OnRemoved(Entity<ModularSuitEmpShieldModuleComponent> module, ref ModularSuitRemovedEvent args)
    {
        Disable(module);
    }

    private void OnToggled(Entity<ModularSuitEmpShieldModuleComponent> module, ref ModularSuitModuleToggledEvent args)
    {
        if (args.Activated)
            Enable(module, args.Suit, args.Wearer);
        else
            Disable(module);
    }

    private void OnShutdown(Entity<ModularSuitEmpShieldModuleComponent> module, ref ComponentShutdown args)
    {
        Disable(module);
    }

    private void Enable(
        Entity<ModularSuitEmpShieldModuleComponent> module,
        EntityUid suit,
        EntityUid? wearer)
    {
        if (wearer is not { } wearerUid || TerminatingOrDeleted(wearerUid))
            return;

        var shield = EnsureComp<ModularSuitEmpShieldedComponent>(wearerUid);
        shield.Suit = suit;
        shield.Module = module.Owner;
        shield.LastEmpTick = uint.MaxValue;
        shield.BlockedLastEmp = false;
        module.Comp.Wearer = wearerUid;
    }

    private void Disable(Entity<ModularSuitEmpShieldModuleComponent> module)
    {
        if (module.Comp.Wearer is not { } wearer)
            return;

        module.Comp.Wearer = null;

        if (TerminatingOrDeleted(wearer) ||
            !TryComp<ModularSuitEmpShieldedComponent>(wearer, out var shield) ||
            shield.Module != module.Owner)
        {
            return;
        }

        RemComp<ModularSuitEmpShieldedComponent>(wearer);
    }

    private void OnEmpAttempt(Entity<MetaDataComponent> target, ref EmpAttemptEvent args)
    {
        if (!TryFindShield(target, out var shield))
            return;

        var tick = _timing.CurTick.Value;
        if (shield.Comp.LastEmpTick != tick)
        {
            shield.Comp.LastEmpTick = tick;
            shield.Comp.BlockedLastEmp = TryPowerShield(shield);
        }

        if (shield.Comp.BlockedLastEmp)
            args.Cancelled = true;
    }

    private bool TryFindShield(
        Entity<MetaDataComponent> target,
        out Entity<ModularSuitEmpShieldedComponent> shield)
    {
        var current = target.Owner;
        var metadata = target.Comp;

        while (true)
        {
            if (TryComp<ModularSuitEmpShieldedComponent>(current, out var shieldComp))
            {
                shield = (current, shieldComp);
                return true;
            }

            // Follow containment only; do not cross map or grid parents.
            if ((metadata.Flags & MetaDataFlags.InContainer) == 0 ||
                !TryComp(current, out TransformComponent? transform) ||
                !transform.ParentUid.Valid ||
                !TryComp(transform.ParentUid, out MetaDataComponent? parentMetadata))
            {
                shield = default;
                return false;
            }

            current = transform.ParentUid;
            metadata = parentMetadata;
        }
    }

    private bool TryPowerShield(Entity<ModularSuitEmpShieldedComponent> shield)
    {
        if (!TryComp<ModularSuitComponent>(shield.Comp.Suit, out var suit) ||
            !suit.Active ||
            suit.Wearer != shield.Owner ||
            !TryComp<ModularSuitModuleComponent>(shield.Comp.Module, out var module) ||
            !module.IsActive)
        {
            return false;
        }

        return _modularSuit.TryUseCoreCharge(
            (shield.Comp.Suit, suit),
            module.PowerInstanceUsage,
            deferDeactivation: true);
    }

    private EntityUid? GetSuitWearer(EntityUid suit)
    {
        return TryComp<ModularSuitComponent>(suit, out var suitComp) ? suitComp.Wearer : null;
    }
}
