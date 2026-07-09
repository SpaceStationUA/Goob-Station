using Content.Pirate.Shared.TerrorSpider.Evolving.Conditions;
using Content.Pirate.Shared.TerrorSpider.Evolving.EntitySystems;
using Content.Shared.Mind;

namespace Content.Pirate.Client.TerrorSpider.Evolving;

public sealed class EvolvingSystem : SharedEvolvingSystem
{
    public override EntityUid TryInitObjectives(EntityUid mindId, MindComponent mind, string objectiveId, EvolvingCondition condition) =>
        base.TryInitObjectives(mindId, mind, objectiveId, condition);
}
