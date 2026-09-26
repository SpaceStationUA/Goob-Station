using Content.Shared._Pirate.MalfAI;
using Content.Shared.Mind;
using Content.Shared.Store;

namespace Content.Server._Pirate.Store.Conditions;

/// <summary>
/// Requires the buyer to have the Doomsday Protocol objective.
/// </summary>
public sealed partial class MalfAiDoomsdayObjectiveCondition : ListingCondition
{
    public override bool Condition(ListingConditionArgs args)
    {
        var ent = args.EntityManager;
        if (!ent.TryGetComponent<MindComponent>(args.Buyer, out var mind) &&
            !ent.System<SharedMindSystem>().TryGetMind(args.Buyer, out _, out mind))
        {
            return false;
        }

        foreach (var objective in mind.Objectives)
        {
            if (ent.TryGetComponent<MalfAiSabotageObjectiveComponent>(objective, out var sabotage) &&
                sabotage.SabotageType == MalfAiSabotageType.Doomsday)
            {
                return true;
            }
        }

        return false;
    }
}
