// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Common.Bloodstream;
using Content.Pirate.Shared.Vampire.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Pirate.Server.Body.Systems;

/// <summary>
/// Pirate: Handles the metabolic cost of blood regeneration.
/// When an entity regenerates blood, it costs hunger and thirst.
/// If the entity is starving, regeneration is reduced.
/// Negative regeneration (blood drain) still costs hunger/thirst.
/// </summary>
public sealed class NaturalBloodRegenerationSystem : EntitySystem
{
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly ThirstSystem _thirst = default!;

    /// <summary>
    /// Base hunger cost per unit of blood regenerated.
    /// </summary>
    public float HungerCostPerUnit = 0.1f;

    /// <summary>
    /// Base thirst cost per unit of blood regenerated.
    /// </summary>
    public float ThirstCostPerUnit = 0.1f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodstreamComponent, BloodRegenerationModifierEvent>(OnBloodRegenerationModifier);
    }

    private void OnBloodRegenerationModifier(Entity<BloodstreamComponent> ent, ref BloodRegenerationModifierEvent args)
    {
        var bloodLevel = _bloodstream.GetBloodLevel((ent.Owner, (BloodstreamComponent?) ent.Comp));
        var bloodIsBelowFull = bloodLevel < 1.0f;
        var amount = args.Amount;

        // Override regen amount with blood deficiency drain.
        if (HasComp<BloodDeficiencyComponent>(ent))
        {
            var deficiency = Comp<BloodDeficiencyComponent>(ent);
            amount = deficiency.DrainPerTick;
        }

        // No regeneration needed when blood is full (and not draining).
        if (!bloodIsBelowFull && amount > 0f)
        {
            args.Amount = 0f;
            return;
        }

        var magnitude = args.Amount;
        var penalty = 1f;

        // Hunger cost
        if(TryComp<HungerComponent>(ent, out var hunger))
        {
            var currentHunger = _hunger.GetHunger(hunger);
            var hungerCost = HungerCostPerUnit * MathF.Abs(magnitude);
            if (currentHunger <= hungerCost)
            {
                penalty *= 0.5f;
            }
            _hunger.ModifyHunger(ent, -hungerCost);
        }

        // Thirst cost
        if (TryComp<ThirstComponent>(ent, out var thirst))
        {
            var thirstCost = ThirstCostPerUnit * MathF.Abs(magnitude);

            if(thirst.CurrentThirst <= thirstCost)
            {
                penalty *= 0.5f;
            }
            _thirst.ModifyThirst(ent, thirst, -thirstCost);
        }
        args.Amount = amount * penalty;
    }
}
