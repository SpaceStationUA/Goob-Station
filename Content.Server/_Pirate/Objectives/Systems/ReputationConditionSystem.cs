using Content.Server._Pirate.Objectives.Components;
using Content.Shared._Pirate.Reputation;
using Content.Shared.Objectives.Components;

namespace Content.Server._Pirate.Objectives.Systems;

/// <summary>
/// Prevents <see cref="ReputationConditionComponent"/> being added if you lack the required reputation.
/// </summary>
public sealed class ReputationConditionSystem : EntitySystem
{
    [Dependency] private readonly ReputationSystem _reputation = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReputationConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
    }

    private void OnAssigned(Entity<ReputationConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        var reputation = _reputation.GetMindReputation(args.MindId) ?? 0;
        if (reputation < ent.Comp.Reputation)
            args.Cancelled = true;
    }
}
