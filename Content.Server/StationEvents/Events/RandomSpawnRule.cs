// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Server.Radio.EntitySystems;
using Content.Server.Pinpointer;
using Robust.Shared.Utility;

namespace Content.Server.StationEvents.Events;

public sealed partial class RandomSpawnRule : StationEventSystem<RandomSpawnRuleComponent>
{
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private RadioSystem _radio = default!;

    protected override void Started(EntityUid uid, RandomSpawnRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (TryFindRandomTile(out _, out _, out _, out var coords))
        {
            Sawmill.Info($"Spawning {comp.Prototype} at {coords}");
            var ent = Spawn(comp.Prototype, coords);

            if (comp.RadioMessage is {} radioMessage)
            {
                var message = Loc.GetString(radioMessage.Message, ("location", FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(ent))));
                _radio.SendRadioMessage(ent, message, radioMessage.Channel, ent);
            }
        }
    }
}