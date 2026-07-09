namespace Content.Pirate.Shared.TerrorSpider.Evolving.Conditions;

public sealed partial class EggsInjectCondition : EvolvingCondition
{
    [DataField]
    public int TargetEggsAmount = 4;

    private int _injectedEggs;

    public override EvolveType Type => EvolveType.EggsInjected;

    public override bool Condition(EvolvingConditionArgs args) => _injectedEggs >= TargetEggsAmount;

    public override int GetTarget() => TargetEggsAmount;

    public bool Condition() => _injectedEggs >= TargetEggsAmount;

    public void UpdateEggs(int eggsAmount) => _injectedEggs += eggsAmount;
}
