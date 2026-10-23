// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.StatusEffectNew;

namespace Content.Server._Pirate.Genetics;

public sealed class GeneticsComponentStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeneticsComponentStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<GeneticsComponentStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnApplied(Entity<GeneticsComponentStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        foreach (var (name, entry) in ent.Comp.Components)
        {
            if (!EntityManager.HasComponent(args.Target, entry.Component.GetType()))
                ent.Comp.Added.TryAdd(name, entry);
        }
        EntityManager.AddComponents(args.Target, ent.Comp.Added, removeExisting: false);
    }

    private void OnRemoved(Entity<GeneticsComponentStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        EntityManager.RemoveComponents(args.Target, ent.Comp.Added);
        ent.Comp.Added.Clear();
    }
}
