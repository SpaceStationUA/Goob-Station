// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Events;
using Content.Pirate.Common.Bloodstream;

namespace Content.Pirate.Shared.Genetics.Abilities;

public sealed class BleedingMutationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BleedingMutationComponent, BleedModifierEvent>(OnBleedModifier);
        SubscribeLocalEvent<BleedingMutationComponent, BloodRegenerationModifierEvent>(OnRegeneration);
    }

    // Pirate: use the existing blood regeneration hook instead of writing a protected
    // bloodstream field. Removing the mutation automatically removes its multiplier.
    private void OnRegeneration(Entity<BleedingMutationComponent> ent, ref BloodRegenerationModifierEvent args)
    {
        args.Amount *= ent.Comp.RefreshModifier;
    }

    private void OnBleedModifier(Entity<BleedingMutationComponent> ent, ref BleedModifierEvent args)
    {
        args.BleedAmount *= ent.Comp.BleedModifier;
    }
}
