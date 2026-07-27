using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Containers;

namespace Content.Pirate.Shared.Silicons.Borgs;

public abstract class SharedBorgModuleLightingSystem : EntitySystem
{
    [Dependency] private readonly SharedRgbLightControllerSystem _rgbSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgModuleLightingComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<BorgModuleLightingComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<BorgModuleComponent>(ent, out var module) && module.InstalledEntity is {} chassis)
            RemComp<RgbLightControllerComponent>(chassis);
    }

    protected void ApplyLighting(EntityUid chassis, Entity<BorgModuleLightingComponent> module)
    {
        if (module.Comp.DiscoMode)
        {
            var rgb = EnsureComp<RgbLightControllerComponent>(chassis);
            _rgbSystem.SetCycleRate(chassis, module.Comp.CycleRate, rgb);
        }
        else
        {
            RemComp<RgbLightControllerComponent>(chassis);
        }
    }

    protected void RemoveLighting(EntityUid chassis)
    {
        RemComp<RgbLightControllerComponent>(chassis);
    }

    protected EntityUid? FindLightingModule(EntityUid chassis)
    {
        if (!TryComp<BorgChassisComponent>(chassis, out var borg))
            return null;

        foreach (var module in borg.ModuleContainer.ContainedEntities)
        {
            if (HasComp<BorgModuleLightingComponent>(module))
                return module;
        }

        return null;
    }
}
