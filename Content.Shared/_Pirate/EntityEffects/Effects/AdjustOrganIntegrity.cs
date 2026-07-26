// SPDX-FileCopyrightText: 2026 Pirate
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.EntityEffects.Effects;

/// <summary>
/// Restores integrity to damaged organs, optionally limited to a single organ slot (e.g. the brain).
/// The amount is split between all matching damaged organs.
/// </summary>
public sealed partial class AdjustOrganIntegrity : EntityEffectBase<AdjustOrganIntegrity>
{
    /// <summary>
    /// Integrity restored per metabolism cycle, split between all matching damaged organs.
    /// </summary>
    [DataField(required: true)]
    public FixedPoint2 Amount = default!;

    /// <summary>
    /// If set, only organs in this slot are affected (e.g. "brain").
    /// </summary>
    [DataField]
    public string? SlotId;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-adjust-organ-integrity", ("amount", Amount));
}

public sealed class AdjustOrganIntegrityEffectSystem : EntityEffectSystem<BodyComponent, AdjustOrganIntegrity>
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly TraumaSystem _trauma = default!;

    /// <summary>
    /// Identifier used for the integrity modifier this effect creates when a damaged
    /// organ has no modifiers left to adjust (e.g. after a partial surgery treatment).
    /// </summary>
    private const string HealingModifierIdentifier = "PirateOrganHealing";

    protected override void Effect(Entity<BodyComponent> ent, ref EntityEffectEvent<AdjustOrganIntegrity> args)
    {
        var amount = args.Effect.Amount * args.Scale;
        if (amount <= FixedPoint2.Zero)
            return;

        var damagedOrgans = new List<Entity<OrganComponent>>();
        foreach (var (organId, organ) in _body.GetBodyOrgans(ent.Owner, ent.Comp))
        {
            if (args.Effect.SlotId is { } slot && organ.SlotId != slot)
                continue;

            if (organ.OrganIntegrity >= organ.IntegrityCap)
                continue;

            damagedOrgans.Add((organId, organ));
        }

        if (damagedOrgans.Count == 0)
            return;

        var amountPerOrgan = amount / damagedOrgans.Count;
        foreach (var (organId, organ) in damagedOrgans)
        {
            var heal = FixedPoint2.Min(amountPerOrgan, organ.IntegrityCap - organ.OrganIntegrity);
            if (heal <= FixedPoint2.Zero)
                continue;

            // Organ integrity is the clamped sum of the organ's integrity modifiers
            // (see TraumaSystem.UpdateOrganIntegrity), so healing means raising that sum.
            // Once integrity is back at the cap the trauma system removes the organ's
            // traumas itself, which unblocks healing the wounds that hold them.
            if (organ.IntegrityModifiers.Count > 0)
            {
                foreach (var (identifier, owner) in organ.IntegrityModifiers.Keys.ToArray())
                {
                    if (_trauma.TryChangeOrganDamageModifier(organId, heal, owner, identifier, organ))
                        break;
                }
            }
            else
            {
                _trauma.TryCreateOrganDamageModifier(organId,
                    organ.OrganIntegrity + heal,
                    organId,
                    HealingModifierIdentifier,
                    organ);
            }
        }
    }
}
