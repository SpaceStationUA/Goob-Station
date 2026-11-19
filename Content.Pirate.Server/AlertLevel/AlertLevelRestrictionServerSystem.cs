// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.AlertLevel;
using Content.Server.AlertLevel;

namespace Content.Pirate.Server.AlertLevel;

public sealed partial class AlertLevelRestrictionServerSystem : EntitySystem
{
    [Dependency] private readonly StationAlertLevelCacheSystem _cache = default!;
    [Dependency] private readonly AlertLevelRestrictionSystem _restriction = default!;

    private readonly List<Entity<AlertLevelRestrictedComponent>> _toRevoke = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent args)
    {
        if (args.Station != _cache.GetMainStation())
            return;

        _toRevoke.Clear();

        var query = EntityQueryEnumerator<AlertLevelRestrictedComponent>();
        while (query.MoveNext(out var uid, out var restricted))
        {
            if (!AlertLevelRestrictionSystem.IsMet(restricted.Requirement, args.AlertLevel))
                _toRevoke.Add((uid, restricted));
        }

        foreach (var ent in _toRevoke)
        {
            _restriction.Revoke(ent);
        }
    }
}
