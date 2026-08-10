using System.Linq;
using Content.Shared.PowerCell;
using Robust.Shared.Containers;

namespace Content.Pirate.Shared.ModularSuit;

public sealed class ModularSuitUiStateSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public bool CanPredictActiveToggle(Entity<ModularSuitComponent> ent, bool active)
    {
        if (!active)
            return false;

        if (!ent.Comp.Assembled)
            return false;

        if (!_container.TryGetContainer(ent, SharedModularSuitSystem.CoreContainer, out var coreContainer)
            || coreContainer.ContainedEntities.Count == 0
            || !TryComp<ModularSuitCoreComponent>(coreContainer.ContainedEntities[0], out var core))
        {
            return false;
        }

        return core.Infinite || core.Charge > 0;
    }

    public bool CanPredictModuleToggle(Entity<ModularSuitComponent> ent, EntityUid module, bool active)
    {
        if (!ent.Comp.Active)
            return false;

        if (!TryComp<ModularSuitModuleComponent>(module, out var moduleComp) || !moduleComp.CanBeDisabled)
            return false;

        if (!active)
            return true;

        if (!TryComp<ModularSuitModuleContainerRequirementComponent>(module, out var requirement))
            return true;

        return _container.TryGetContainer(module, requirement.RequiredContainerId, out var required)
            && required.ContainedEntities.Count > 0;
    }

    public ModularSuitBoundUserInterfaceState BuildUiState(Entity<ModularSuitComponent> ent)
    {
        float coreCharge = 0;
        float maxCoreCharge = 100;
        var coreMultiplier = 1.0f;
        var infinityCore = false;
        var hasCore = false;

        if (_container.TryGetContainer(ent, SharedModularSuitSystem.CoreContainer, out var coreContainer)
            && coreContainer.ContainedEntities.Count > 0
            && TryComp<ModularSuitCoreComponent>(coreContainer.ContainedEntities[0], out var core))
        {
            coreCharge = core.Charge;
            maxCoreCharge = core.MaxCharge;
            coreMultiplier = core.DrawMultiplier;
            infinityCore = core.Infinite;
            hasCore = true;
        }

        var hasBattery = false;
        float batteryCharge = 0;
        float maxBatteryCharge = 0;
        if (_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery))
        {
            batteryCharge = battery.Value.Comp.LastCharge;
            maxBatteryCharge = battery.Value.Comp.MaxCharge;
            hasBattery = true;
        }

        var totalPowerDraw = ent.Comp.BasePowerDraw;
        var modules = new List<SuitModuleEntry>();

        if (_container.TryGetContainer(ent, SharedModularSuitSystem.ModuleContainer, out var moduleContainer))
        {
            foreach (var moduleUid in moduleContainer.ContainedEntities)
            {
                if (!TryComp<ModularSuitModuleComponent>(moduleUid, out var module))
                    continue;

                if (module.IsActive)
                    totalPowerDraw += module.PowerUsage;

                modules.Add(new SuitModuleEntry(
                    GetNetEntity(moduleUid),
                    Name(moduleUid),
                    module.ModuleId,
                    module.IsActive,
                    module.IsPermanent,
                    module.PowerUsage,
                    module.PowerInstanceUsage,
                    module.CanBeDisabled,
                    module.Tags.Select(t => t.ToString()).ToList()
                ));
            }
        }

        var parts = new List<SuitPartEntry>();

        if (_container.TryGetContainer(ent, SharedModularSuitSystem.PartContainer, out var partContainer))
        {
            foreach (var partUid in partContainer.ContainedEntities)
            {
                if (!TryComp<ModularSuitPartComponent>(partUid, out var part))
                    continue;

                parts.Add(new SuitPartEntry(GetNetEntity(partUid), Name(partUid), part.PartType));
            }
        }

        if (TryComp<ModularSuitEquippedComponent>(ent, out var equipped))
        {
            foreach (var (_, partUid) in equipped.EquippedParts)
            {
                if (!TryComp<ModularSuitPartComponent>(partUid, out var part))
                    continue;

                parts.Add(new SuitPartEntry(GetNetEntity(partUid), Name(partUid), part.PartType));
            }
        }

        string? wearerName = null;
        if (ent.Comp.Wearer != null)
            wearerName = Name(ent.Comp.Wearer.Value);

        totalPowerDraw *= coreMultiplier;

        return new ModularSuitBoundUserInterfaceState(
            ent.Comp.Active,
            coreCharge,
            maxCoreCharge,
            hasCore,
            infinityCore,
            hasBattery,
            batteryCharge,
            maxBatteryCharge,
            totalPowerDraw,
            modules,
            parts,
            wearerName
        );
    }
}
