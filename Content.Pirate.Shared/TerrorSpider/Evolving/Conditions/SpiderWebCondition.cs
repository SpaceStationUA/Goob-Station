namespace Content.Pirate.Shared.TerrorSpider.Evolving.Conditions;

public sealed partial class SpiderWebCondition : EvolvingCondition
{
    [DataField]
    public int TargetWebAmount = 24;

    private int _createdWebs;

    public override EvolveType Type => EvolveType.SpiderWebsSpawned;

    public override bool Condition(EvolvingConditionArgs args) => _createdWebs >= TargetWebAmount;

    public override int GetTarget() => TargetWebAmount;

    public bool Condition() => _createdWebs >= TargetWebAmount;

    public void UpdateWebs(int websAmount) => _createdWebs += websAmount;
}
