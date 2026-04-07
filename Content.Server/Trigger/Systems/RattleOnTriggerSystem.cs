using Content.Server.Radio.EntitySystems;
using Content.Server.Pinpointer;
using Content.Shared.Mobs.Components;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.Radio; // Pirates: death rattle update
using Content.Server.Station.Systems; // Pirates: death rattle update
using Robust.Shared.Map; // Pirates: death rattle update

namespace Content.Server.Trigger.Systems;

public sealed class RattleOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!; // Pirates: death rattle update
    [Dependency] private readonly StationSystem _station = default!; // Pirates: death rattle update

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RattleOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<RattleOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var target = ent.Comp.TargetUser ? args.User : ent.Owner;

        if (target == null)
            return;

        if (!TryComp<MobStateComponent>(target.Value, out var mobstate))
            return;

        args.Handled = true;

        if (!ent.Comp.Messages.TryGetValue(mobstate.CurrentState, out var messageId))
            return;

        // Gets the location of the user
        var pos = _transform.GetMapCoordinates(target.Value); // Pirates: death rattle update
        var posText = GetPositionText(pos, ent.Comp.ReportCoordinates); // Pirates: death rattle update

        var message = Loc.GetString(messageId, ("user", target.Value), ("position", posText));
        // Sends a message to the radio channel specified by the implant
        _radio.SendRadioMessage(ent.Owner, message, _prototypeManager.Index(ent.Comp.RadioChannel), ent.Owner);
        #region Pirates: death rattle update
        if (!ent.Comp.RelayToStationWhenOffStation || _station.GetOwningStation(target.Value) != null)
            return;

        if (TryGetRelaySourceGrid(out var relaySource))
        {
            var sentRelayChannels = new HashSet<ProtoId<RadioChannelPrototype>>();
            var sameMapAsRelaySource = pos.MapId == Transform(relaySource).MapID;

            foreach (var relayChannel in ent.Comp.OffStationRelayChannels)
            {
                // Avoid duplicate same-channel broadcasts when off-station but still on the station map.
                if (sameMapAsRelaySource && relayChannel == ent.Comp.RadioChannel)
                    continue;

                if (!sentRelayChannels.Add(relayChannel))
                    continue;

                _radio.SendRadioMessage(ent.Owner, message, _prototypeManager.Index(relayChannel), relaySource);
            }
        }
        #endregion
    }
    #region Pirates: death rattle update
    private string GetPositionText(MapCoordinates pos, bool reportCoordinates)
    {
        var location = CapitalizeFirst(
            FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(pos)),
            Loc.GetString("rattle-on-trigger-unknown-position"));
        if (!reportCoordinates)
            return location;

        if (pos.MapId == MapId.Nullspace)
            return location;

        var x = (int)pos.Position.X;
        var y = (int)pos.Position.Y;
        return $"{location} ({x}, {y})";
    }

    private static string CapitalizeFirst(string text, string fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private bool TryGetRelaySourceGrid(out EntityUid relaySource)
    {
        foreach (var station in _station.GetStations())
        {
            if (_station.GetLargestGrid(station) is { } grid)
            {
                relaySource = grid;
                return true;
            }
        }

        relaySource = EntityUid.Invalid;
        return false;
    }
    #endregion
}
