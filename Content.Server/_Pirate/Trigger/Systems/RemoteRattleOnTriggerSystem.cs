// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Power.Components;
using Content.Server.Radio.EntitySystems;
using Content.Server.Trigger.Systems;
using Content.Shared._Pirate.Trigger.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Radio.Components;
using Content.Shared.Trigger;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Trigger.Systems;

public sealed class RemoteRattleOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly RattleOnTriggerSystem _rattle = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RemoteRattleOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<RemoteRattleOnTriggerComponent> ent, ref TriggerEvent args)
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

        var channel = _prototype.Index(ent.Comp.RadioChannel);
        var pos = _transform.GetMapCoordinates(target.Value);
        var posText = _rattle.GetPositionText(pos, ent.Comp.ReportCoordinates);
        var message = Loc.GetString(messageId, ("user", target.Value), ("position", posText));

        var sentMaps = new HashSet<MapId>();
        var servers = EntityQueryEnumerator<TelecomServerComponent,
            EncryptionKeyHolderComponent,
            ApcPowerReceiverComponent,
            TransformComponent>();

        while (servers.MoveNext(out var server, out _, out var keys, out var power, out var xform))
        {
            if (!power.Powered || !keys.Channels.Contains(channel.ID))
                continue;

            if (!sentMaps.Add(xform.MapID))
                continue;

            _radio.SendRadioMessage(ent.Owner, message, channel, server);
        }
    }
}
