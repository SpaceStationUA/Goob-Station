using Content.Shared._Shitmed.Damage;
using Content.Shared._Shitmed.EntityEffects.Effects;
using Content.Shared._Shitmed.Targeting;

namespace Content.Shared.EntityEffects.Effects.Damage;

// Pirate: preserve downstream reagent fields across the upstream damage refactor.
public sealed partial class EvenHealthChange
{
    [DataField]
    public SplitDamageBehavior SplitDamage = SplitDamageBehavior.SplitEnsureAllOrganic;

    [DataField]
    public bool UseTargeting = true;

    [DataField]
    public TargetBodyPart TargetPart = TargetBodyPart.All;

    [DataField]
    public TemperatureScaling? ScaleByTemperature;
}
