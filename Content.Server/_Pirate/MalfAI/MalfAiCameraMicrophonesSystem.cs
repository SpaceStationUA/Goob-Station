// SPDX-FileCopyrightText: 2025 Terkala <appleorange64@gmail.com>
// SPDX-FileCopyrightText: 2025 Tyranex <bobthezombie4@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Systems;
using Content.Server.SurveillanceCamera;
using Content.Shared.Speech.Components;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared._Pirate.MalfAI;
using Content.Shared.GameTicking;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using static Content.Server.Chat.Systems.ChatSystem;

namespace Content.Server._Pirate.MalfAI;

/// <summary>
/// Relays local IC speech to a Malf AI whose active core is near an enabled camera microphone.
/// </summary>
public sealed class MalfAiCameraMicrophonesSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly Content.Server.Silicons.StationAi.StationAiSystem _stationAi = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandRecipients);
        SubscribeLocalEvent<MalfAiMarkerComponent, MalfAiCameraMicrophonesUnlockedEvent>(OnCameraMicrophonesUnlocked);
        SubscribeLocalEvent<StationAiHeldComponent, ComponentStartup>(OnHeldStartup);
        SubscribeLocalEvent<StationAiHeldComponent, ComponentShutdown>(OnHeldShutdown);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnCameraMicrophonesUnlocked(EntityUid uid, MalfAiMarkerComponent marker, MalfAiCameraMicrophonesUnlockedEvent ev)
    {
        var comp = EnsureComp<MalfAiCameraMicrophonesComponent>(uid);
        comp.EnabledDesired = true;
        // Actual core connectivity is checked when relaying speech, so buying while carded
        // does not permanently disable the upgrade after the AI returns to its core.
        comp.EnabledEffective = HasComp<StationAiHeldComponent>(uid);
        Dirty(uid, comp);
    }

    private void OnHeldStartup(EntityUid uid, StationAiHeldComponent held, ref ComponentStartup args)
    {
        if (TryComp(uid, out MalfAiCameraMicrophonesComponent? microphones))
        {
            microphones.EnabledEffective = microphones.EnabledDesired;
            Dirty(uid, microphones);
        }
    }

    private void OnHeldShutdown(EntityUid uid, StationAiHeldComponent held, ref ComponentShutdown args)
    {
        if (TryComp(uid, out MalfAiCameraMicrophonesComponent? microphones) && microphones.EnabledEffective)
        {
            microphones.EnabledEffective = false;
            Dirty(uid, microphones);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        var microphoneQuery = EntityQueryEnumerator<MalfAiCameraMicrophonesComponent>();
        while (microphoneQuery.MoveNext(out var uid, out var microphones))
        {
            microphones.EnabledEffective = false;
            microphones.EnabledDesired = false;
            Dirty(uid, microphones);
        }
    }



    private void OnExpandRecipients(ExpandICChatRecipientsEvent ev)
    {
        if (ev.VoiceRange <= 0f || !TryComp(ev.Source, out TransformComponent? sourceXform))
            return;

        var sourceCoordinates = sourceXform.Coordinates;
        var aiQuery = EntityQueryEnumerator<MalfAiMarkerComponent, MalfAiCameraMicrophonesComponent, ActorComponent>();
        while (aiQuery.MoveNext(out var aiUid, out _, out var microphoneUpgrade, out var actor))
        {
            if (!microphoneUpgrade.EnabledEffective || !_stationAi.TryGetCore(aiUid, out var core))
                continue;

            if (core.Comp?.RemoteEntity is not { } eye || !TryComp(eye, out TransformComponent? eyeXform))
                continue;

            var cameraQuery = EntityQueryEnumerator<SurveillanceCameraMicrophoneComponent,
                ActiveListenerComponent, SurveillanceCameraComponent, TransformComponent>();
            var heard = false;
            var closestSourceDistance = float.MaxValue;

            while (cameraQuery.MoveNext(out _, out var cameraMicrophone, out var listener, out var camera, out var cameraXform))
            {
                if (!camera.Active || !cameraMicrophone.Enabled)
                    continue;

                if (!cameraXform.Coordinates.TryDistance(EntityManager, eyeXform.Coordinates, out var eyeDistance) ||
                    eyeDistance > microphoneUpgrade.RadiusTiles)
                    continue;

                if (!cameraXform.Coordinates.TryDistance(EntityManager, sourceCoordinates, out var sourceDistance) ||
                    sourceDistance > ev.VoiceRange || sourceDistance > listener.Range)
                    continue;

                heard = true;
                closestSourceDistance = MathF.Min(closestSourceDistance, sourceDistance);
            }

            if (heard)
            {
                // TryAdd preserves normal local-chat delivery when the AI is already in range,
                // while ensuring multiple cameras cannot duplicate one message.
                ev.Recipients.TryAdd(actor.PlayerSession,
                    new ICChatRecipientData(closestSourceDistance, false, false, InLOS: true));
            }
        }
    }
}
