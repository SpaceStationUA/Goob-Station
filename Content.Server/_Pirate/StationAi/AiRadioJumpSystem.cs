// SPDX-FileCopyrightText: 2025 Moffstation
// SPDX-License-Identifier: MIT

using Content.Server.Radio;
using Content.Server.Holopad;
using Content.Shared.Holopad;
using Content.Shared._Pirate.StationAi;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.StationAi;

public sealed class AiRadioJumpSystem : EntitySystem
{
    [Dependency] private readonly HolopadSystem _holopads = default!;
    [Dependency] private readonly SharedStationAiSystem _ai = default!;
    [Dependency] private readonly SharedSuitSensorSystem _sensors = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationAiHeldComponent, RadioReceiveEvent>(OnRadioReceive);
        SubscribeNetworkEvent<AiRadioJumpEvent>(OnJump);
    }

    private void OnRadioReceive(Entity<StationAiHeldComponent> ent, ref RadioReceiveEvent args)
    {
        if (HasComp<ActorComponent>(ent))
            EnsureComp<AiRadioHistoryComponent>(ent).Speakers.Add(args.MessageSource);
    }

    private void OnJump(AiRadioJumpEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } ai ||
            !HasComp<StationAiHeldComponent>(ai) || !TryComp<AiRadioHistoryComponent>(ai, out var history) ||
            history.NextJump > _timing.CurTime || !TryGetEntity(msg.Speaker, out var speaker) ||
            !history.Speakers.Contains(speaker.Value))
            return;

        history.NextJump = _timing.CurTime + TimeSpan.FromMilliseconds(500);
        if (!_ai.TryGetCore(ai, out var core) || core.Comp?.RemoteEntity == null)
            return;
        if (Transform(core).GridUid is not { } grid || Transform(speaker.Value).GridUid != grid)
        {
            _popup.PopupEntity(Loc.GetString("pirate-ai-radio-out-of-range"), ai, ai);
            return;
        }

        if (speaker != core.Owner && !_sensors.HasPublicCoordinates(speaker.Value))
        {
            _popup.PopupEntity(Loc.GetString("pirate-ai-radio-no-coordinates"), ai, ai);
            return;
        }

        if (TryComp<HolopadUserComponent>(ai, out var call))
            _holopads.EndHolocalls((ai, call));

        // Ending the call can replace the eye when the core returns to roaming mode.
        if (core.Comp.RemoteEntity is { } eye)
            _transform.DropNextTo(eye, speaker.Value);
    }
}
