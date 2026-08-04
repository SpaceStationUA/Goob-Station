using Content.Shared.Implants;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Implants.AddComponentsImplant;

public sealed class AddComponentsImplantSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AddComponentsImplantComponent, ImplantImplantedEvent>(OnImplantImplantedEvent);
        SubscribeLocalEvent<AddComponentsImplantComponent, EntGotRemovedFromContainerMessage>(OnRemove);
    }

    private void OnImplantImplantedEvent(Entity<AddComponentsImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        if (!(args.Implanted is { } target))
        {
            return;
        }

        // Pirate: local EntityManager adds registry entries in bulk, not raw component instances.
        var added = new ComponentRegistry();

        foreach (var component in ent.Comp.ComponentsToAdd)
        {
            // Don't add the component if it already exists
            if (EntityManager.HasComponent(target, component.Value.Component.GetType()))
                continue;

            added.Add(component.Key, component.Value);
        }

        EntityManager.AddComponents(target, added);

        foreach (var component in added)
        {
            ent.Comp.AddedComponents.Add(component.Key, component.Value);
        }
    }

    private void OnRemove(Entity<AddComponentsImplantComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        EntityManager.RemoveComponents(args.Container.Owner, ent.Comp.AddedComponents);

        // Clear the list so the implant can be reused.
        ent.Comp.AddedComponents.Clear();
    }
}
