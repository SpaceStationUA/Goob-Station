using Content.Server._Pirate.Objectives.Systems;
using Content.Shared.Mind;
using Content.Shared.Store;

namespace Content.Server._Pirate.Store.Conditions;

/// <summary>
/// Requires the buyer to have an active objective that unlocks this listing.
/// </summary>
public sealed partial class ObjectiveUnlockCondition : ListingCondition
{
    public override bool Condition(ListingConditionArgs args)
    {
        if (!args.EntityManager.TryGetComponent<MindComponent>(args.Buyer, out var mind))
            return false;

        var unlocker = args.EntityManager.System<StoreUnlockerSystem>();
        return unlocker.IsUnlocked(mind, args.Listing.ID);
    }
}
