using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Server.StationEvents.SecretPlus;

public sealed class PirateSecretPlusRuleFilterEvent
{
    public EntityUid Scheduler { get; }
    public ProtoId<EntityPrototype> Rule { get; }
    public EntityPrototype Prototype { get; }
    public int PlayerCount { get; }
    public bool Cancelled { get; set; }

    public PirateSecretPlusRuleFilterEvent(
        EntityUid scheduler,
        ProtoId<EntityPrototype> rule,
        EntityPrototype prototype,
        int playerCount)
    {
        Scheduler = scheduler;
        Rule = rule;
        Prototype = prototype;
        PlayerCount = playerCount;
    }
}

public sealed class PirateSecretPlusRuleStartedEvent
{
    public EntityUid Scheduler { get; }
    public EntityUid RuleEntity { get; }
    public int PlayerCount { get; }

    public PirateSecretPlusRuleStartedEvent(
        EntityUid scheduler,
        EntityUid ruleEntity,
        int playerCount)
    {
        Scheduler = scheduler;
        RuleEntity = ruleEntity;
        PlayerCount = playerCount;
    }
}

public sealed class PirateSecretPlusPrimarySelectionEvent
{
    public EntityUid Scheduler { get; }
    public Dictionary<string, float> Weights { get; }
    public bool Cancelled { get; set; }

    public PirateSecretPlusPrimarySelectionEvent(EntityUid scheduler, Dictionary<string, float> weights)
    {
        Scheduler = scheduler;
        Weights = weights;
    }
}
