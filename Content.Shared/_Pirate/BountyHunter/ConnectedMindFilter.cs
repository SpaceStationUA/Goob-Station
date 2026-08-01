using Content.Shared.Mind;
using Content.Shared.Mind.Filters;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Shared._Pirate.BountyHunter;

/// <summary>
/// Keeps objective targets whose player session is actively in game.
/// </summary>
public sealed partial class ConnectedMindFilter : MindFilter
{
    private static ISharedPlayerManager? _playerManager;

    protected override bool ShouldRemove(Entity<MindComponent> mind, EntityUid? exclude, IEntityManager entMan,
        SharedMindSystem mindSys)
    {
        _playerManager ??= IoCManager.Resolve<ISharedPlayerManager>();

        return mind.Comp.UserId is not { } userId ||
               !_playerManager.TryGetSessionById(userId, out var session) ||
               session.Status != SessionStatus.InGame;
    }
}
