// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;

namespace Content.Pirate.Shared.Genetics.Mutations;

public sealed class ComponentMutationSystem : EntitySystem
{
    [Dependency] private readonly ISerializationManager _serialization = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Upgrades must release the old mutation's components before taking ownership of replacements.
        SubscribeLocalEvent<ComponentMutationComponent, MutationAddedEvent>(OnAdded,
            after: new[] { typeof(RemovesMutationSystem) });
        SubscribeLocalEvent<ComponentMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<ComponentMutationComponent> ent, ref MutationAddedEvent args)
    {
        if (ent.Comp.Added is {} added)
        {
            foreach (var (name, entry) in added)
            {
                if (!EntityManager.HasComponent(args.Target, entry.Component.GetType()))
                    ent.Comp.AddedComponents.Add(name, entry);
            }
            EntityManager.AddComponents(args.Target, ent.Comp.AddedComponents, removeExisting: false);
        }
        if (ent.Comp.Removed is {} removed)
        {
            foreach (var (name, entry) in removed)
            {
                if (EntityManager.TryGetComponent(args.Target, entry.Component.GetType(), out var component))
                {
                    var copy = _serialization.CreateCopy(component, notNullableOverride: true);
                    ent.Comp.RemovedComponents.Add(name, new EntityPrototype.ComponentRegistryEntry(copy));
                }
            }
            EntityManager.RemoveComponents(args.Target, ent.Comp.RemovedComponents);
        }
    }

    private void OnRemoved(Entity<ComponentMutationComponent> ent, ref MutationRemovedEvent args)
    {
        // Restore only components this mutation removed, without overwriting later additions.
        EntityManager.AddComponents(args.Target, ent.Comp.RemovedComponents, removeExisting: false);
        EntityManager.RemoveComponents(args.Target, ent.Comp.AddedComponents);
        ent.Comp.RemovedComponents.Clear();
        ent.Comp.AddedComponents.Clear();
    }
}
