using Content.Pirate.Shared.ModularSuit;

namespace Content.Pirate.Server.ModularSuit;

public sealed partial class ModularSuitWearerEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModularSuitModuleWearerEffectComponent, ModularSuitInstalledEvent>(OnModuleInstalled);
        SubscribeLocalEvent<ModularSuitModuleWearerEffectComponent, ModularSuitRemovedEvent>(OnModuleRemoved);
        SubscribeLocalEvent<ModularSuitModuleWearerEffectComponent, ModularSuitModuleToggledEvent>(OnModuleToggled);
    }

    private void OnModuleInstalled(Entity<ModularSuitModuleWearerEffectComponent> module, ref ModularSuitInstalledEvent args)
    {
        if (TryComp<ModularSuitModuleComponent>(module.Owner, out var moduleComp) && moduleComp.IsActive)
            ApplyEffects(GetSuitWearer(args.Suit), module.Comp);
    }

    private void OnModuleRemoved(Entity<ModularSuitModuleWearerEffectComponent> module, ref ModularSuitRemovedEvent args)
    {
        if (module.Comp.ActiveComponents != null)
        {
            RemoveEffects(GetSuitWearer(args.Suit), module.Comp);
        }
    }

    private void OnModuleToggled(Entity<ModularSuitModuleWearerEffectComponent> module, ref ModularSuitModuleToggledEvent args)
    {
        if (args.Activated)
        {
            ApplyEffects(args.Wearer, module.Comp);
        }
        else
        {
            RemoveEffects(args.Wearer, module.Comp);
        }
    }

    private void ApplyEffects(EntityUid? user, ModularSuitModuleWearerEffectComponent component)
    {
        if (user == null || component.ActiveComponents == null)
            return;

        EntityManager.AddComponents(user.Value, component.ActiveComponents);

        // Sync
        foreach (var (_, entry) in component.ActiveComponents)
        {
            var compType = entry.Component.GetType();
            if (EntityManager.TryGetComponent(user.Value, compType, out var comp))
            {
                var reg = EntityManager.ComponentFactory.GetRegistration(compType);
                if (reg.NetID != null)
                {
                    Dirty(user.Value, comp);
                }
            }
        }
    }

    private void RemoveEffects(EntityUid? user, ModularSuitModuleWearerEffectComponent component)
    {
        if (user == null || component.ActiveComponents == null)
            return;

        EntityManager.RemoveComponents(user.Value, component.ActiveComponents);

        if (component.ReturnedComponents != null)
        {
            EntityManager.AddComponents(user.Value, component.ReturnedComponents);
        }
    }

    // Pirate: the module installer is not necessarily the person wearing the suit.
    private EntityUid? GetSuitWearer(EntityUid suit)
    {
        return TryComp<ModularSuitComponent>(suit, out var suitComp) ? suitComp.Wearer : null;
    }
}
