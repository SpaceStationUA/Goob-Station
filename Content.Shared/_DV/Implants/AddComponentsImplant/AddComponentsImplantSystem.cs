using Content.Shared.Implants;
using Robust.Shared.Containers;

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
        var target = args.Implanted;

        foreach (var component in ent.Comp.ComponentsToAdd)
        {
            // Don't add the component if it already exists
            if (EntityManager.HasComponent(target, component.Value.Component.GetType()))
                continue;

            EntityManager.AddComponent(target, component.Value);
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
