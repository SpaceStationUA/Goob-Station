// SPDX-FileCopyrightText: 2025 Terkala <appleorange64@gmail.com>
// SPDX-FileCopyrightText: 2025 Tyranex <bobthezombie4@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Systems;
using Content.Server.SurveillanceCamera;
using Content.Shared.StationAi;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared._Pirate.MalfAI;
using Content.Shared.GameTicking;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using static Content.Server.Chat.Systems.ChatSystem;

namespace Content.Server._Pirate.MalfAI;

/// <summary>
/// Relays local IC speech through cameras near the Malf AI's eye.
/// </summary>
public sealed class MalfAiCameraMicrophonesSystem : EntitySystem
{
    [Dependency] private readonly Content.Server.Silicons.StationAi.StationAiSystem _stationAi = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandRecipients);
        SubscribeLocalEvent<MalfAiMarkerComponent, MalfAiCameraMicrophonesUnlockedEvent>(OnCameraMicrophonesUnlocked);
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

            // This purchase supplies microphones to ordinary station cameras too.
            var cameraQuery = EntityQueryEnumerator<StationAiVisionComponent, SurveillanceCameraComponent, TransformComponent>();
            var heard = false;
            var closestSourceDistance = float.MaxValue;

            while (cameraQuery.MoveNext(out var cameraUid, out var vision, out var camera, out var cameraXform))
            {
                if (!camera.Active || !vision.Enabled ||
                    (vision.NeedsPower && !_power.IsPowered(cameraUid)) ||
                    (vision.NeedsAnchoring && !cameraXform.Anchored))
                    continue;

                var hearingRange = ev.VoiceRange;
                if (TryComp<SurveillanceCameraMicrophoneComponent>(cameraUid, out var microphone))
                {
                    if (!microphone.Enabled)
                        continue;
                    hearingRange = MathF.Min(hearingRange, microphone.Range);
                }

                if (!cameraXform.Coordinates.TryDistance(EntityManager, eyeXform.Coordinates, out var eyeDistance) ||
                    eyeDistance > microphoneUpgrade.RadiusTiles)
                    continue;

                if (!cameraXform.Coordinates.TryDistance(EntityManager, sourceCoordinates, out var sourceDistance) ||
                    sourceDistance > hearingRange)
                    continue;

                heard = true;
                closestSourceDistance = MathF.Min(closestSourceDistance, sourceDistance);
            }

            if (heard)
            {
                // An existing recipient can be out of LOS or bubble-only. Upgrade that delivery too.
                if (ev.Recipients.TryGetValue(actor.PlayerSession, out var existing))
                    ev.Recipients[actor.PlayerSession] = existing with
                    {
                        Range = MathF.Min(existing.Range, closestSourceDistance),
                        HideChatOverride = false,
                        InLOS = true,
                    };
                else
                    ev.Recipients.Add(actor.PlayerSession,
                        new ICChatRecipientData(closestSourceDistance, false, false, InLOS: true));
            }
        }
    }
}
