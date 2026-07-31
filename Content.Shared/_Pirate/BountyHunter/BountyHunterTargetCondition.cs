using Content.Shared.EntityTable;
using Content.Shared.EntityTable.Conditions;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.BountyHunter;

/// <summary>
/// Allows the Bounty Hunter event only when a live, eligible InGame antagonist exists.
/// </summary>
public sealed partial class BountyHunterTargetCondition : EntityTableCondition
{
    private const string ObjectiveImmuneComponentName = "TargetObjectiveImmune";
    private static ISharedPlayerManager? _playerManager;

    protected override bool EvaluateImplementation(EntityTableSelector root, IEntityManager entMan,
        IPrototypeManager proto, EntityTableContext ctx)
    {
        _playerManager ??= IoCManager.Resolve<ISharedPlayerManager>();

        var minds = entMan.System<SharedMindSystem>().GetAliveHumans();
        var roles = entMan.System<SharedRoleSystem>();

        foreach (var mind in minds)
        {
            if (!roles.MindIsAntagonist(mind))
                continue;

            if (IsObjectiveImmune(mind, entMan))
                continue;

            if (mind.Comp.UserId is { } userId &&
                _playerManager.TryGetSessionById(userId, out var session) &&
                session.Status == SessionStatus.InGame)
                return true;
        }

        return false;
    }

    private static bool IsObjectiveImmune(Entity<MindComponent> mind, IEntityManager entMan)
    {
        // The marker is server-only, while entity table conditions must live in Content.Shared.
        if (!entMan.ComponentFactory.TryGetRegistration(ObjectiveImmuneComponentName, out var registration))
            return false;

        return entMan.HasComponent(mind.Owner, registration.Type) ||
               mind.Comp.OwnedEntity is { } body && entMan.HasComponent(body, registration.Type);
    }
}
