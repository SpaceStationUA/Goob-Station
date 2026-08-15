namespace Content.Shared._Pirate.Objectives.Systems;

public abstract class SharedContractObjectiveSystem : EntitySystem
{
    public virtual string ContractName(EntityUid objective)
    {
        return Name(objective);
    }
}
