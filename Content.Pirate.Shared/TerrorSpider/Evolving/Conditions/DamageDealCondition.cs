namespace Content.Pirate.Shared.TerrorSpider.Evolving.Conditions;

public sealed partial class DamageDealCondition : EvolvingCondition
{
    [DataField]
    public float TargetDamageAmount = 10f;

    [DataField]
    public bool OnlyAlive;

    private float _dealtDamage;

    public override EvolveType Type => EvolveType.DamageDeal;

    public override bool Condition(EvolvingConditionArgs args) => _dealtDamage >= TargetDamageAmount;

    public override int GetTarget() => (int) TargetDamageAmount;

    public bool Condition() => _dealtDamage >= TargetDamageAmount;

    public void AddDamage(float dealtDamage) => _dealtDamage += dealtDamage;
}
