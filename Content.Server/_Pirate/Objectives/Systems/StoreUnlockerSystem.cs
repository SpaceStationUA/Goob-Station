using Content.Server._Pirate.Objectives.Components;
using Content.Shared.Mind;

namespace Content.Server._Pirate.Objectives.Systems;

/// <summary>
/// Provides the API used by objective-unlocked uplink listings.
/// </summary>
public sealed class StoreUnlockerSystem : EntitySystem
{
    private EntityQuery<StoreUnlockerComponent> _query;

    public override void Initialize()
    {
        base.Initialize();
        _query = GetEntityQuery<StoreUnlockerComponent>();
    }

    public bool IsUnlocked(MindComponent mind, string id)
    {
        foreach (var objective in mind.Objectives)
        {
            if (!_query.TryComp(objective, out var unlocker))
                continue;

            if (unlocker.Listings.Contains(id))
                return true;
        }

        return false;
    }
}
