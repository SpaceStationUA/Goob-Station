// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Speech;
using Content.Goobstation.Shared.Supermatter.Components;
using Content.Goobstation.Shared.Supermatter.Monitor;
using Content.Server.Audio;
using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using GoobSupermatterSystem = Content.Goobstation.Server.Supermatter.Systems.SupermatterSystem;

namespace Content.Pirate.Server.Supermatter;

public sealed class PirateSupermatterSoundSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AmbientSoundSystem _ambient = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesBefore.Add(typeof(GoobSupermatterSystem));

        SubscribeLocalEvent<PirateSupermatterSoundComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PirateSupermatterSoundComponent, GetSpeechSoundEvent>(OnGetSpeechSound);
    }

    private void OnMapInit(Entity<PirateSupermatterSoundComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<SupermatterComponent>(ent, out var supermatter))
            return;

        supermatter.LightningPrototypes = ent.Comp.LightningPrototypes;

        if (TryComp<AmbientSoundComponent>(ent, out var ambient))
        {
            _ambient.SetRange(ent, ent.Comp.SoundRange, ambient);
            _ambient.SetVolume(ent, ent.Comp.InitialVolume, ambient);
            _ambient.SetSound(ent, ent.Comp.CalmLoopSound, ambient);
        }

        var status = GetStatus(supermatter, ent.Comp);
        ent.Comp.LastStatus = status;
        ent.Comp.NextSoundUpdate = _timing.CurTime;
    }

    private void OnGetSpeechSound(Entity<PirateSupermatterSoundComponent> ent, ref GetSpeechSoundEvent args)
    {
        if (!TryComp<SupermatterComponent>(ent, out var supermatter))
            return;

        var status = GetStatus(supermatter, ent.Comp);
        args.SpeechSoundProtoId = status switch
        {
            SupermatterStatusType.Warning => ent.Comp.WarningSpeechSound,
            SupermatterStatusType.Danger => ent.Comp.DangerSpeechSound,
            SupermatterStatusType.Emergency => ent.Comp.EmergencySpeechSound,
            SupermatterStatusType.Delaminating => ent.Comp.DelamSpeechSound,
            _ => null,
        };
        args.Handled = true;

        if (TryComp<SpeechComponent>(ent, out var speech))
        {
            speech.AudioParams = status == SupermatterStatusType.Warning
                ? AudioParams.Default.WithVolume(7.5f)
                : AudioParams.Default.WithVolume(10f);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PirateSupermatterSoundComponent, SupermatterComponent>();
        while (query.MoveNext(out var uid, out var sound, out var supermatter))
        {
            var status = GetStatus(supermatter, sound);

            HandleStatusChange(uid, status, sound);
            HandleDistortion(uid, supermatter, sound);

            if (_timing.CurTime < sound.NextSoundUpdate)
                continue;

            sound.NextSoundUpdate = _timing.CurTime + sound.SoundUpdateInterval;

            if (TryComp<AmbientSoundComponent>(uid, out var ambient))
                HandleSoundLoop(uid, supermatter, status, sound, ambient);

            HandleAccent(uid, supermatter, status, sound);
        }
    }

    private void HandleStatusChange(
        EntityUid uid,
        SupermatterStatusType status,
        PirateSupermatterSoundComponent sound)
    {
        var previousStatus = sound.LastStatus;
        sound.LastStatus = status;

        if (previousStatus == null || previousStatus == status)
            return;

        // Goob uses a station announcement for these states, so play the local SM alarm explicitly.
        if (status == SupermatterStatusType.Emergency)
            _audio.PlayPvs(sound.EmergencyStatusSound, uid, AudioParams.Default.WithVolume(10f));
        else if (status == SupermatterStatusType.Delaminating)
            _audio.PlayPvs(sound.DelamStatusSound, uid, AudioParams.Default.WithVolume(10f));
    }

    private void HandleDistortion(
        EntityUid uid,
        SupermatterComponent supermatter,
        PirateSupermatterSoundComponent sound)
    {
        if (!supermatter.Delamming)
        {
            sound.DistortPlayed = false;
            return;
        }

        var playAt = Math.Max(supermatter.DelamTimer - supermatter.UpdateTimer, 0f);
        if (sound.DistortPlayed || supermatter.DelamTimerAccumulator < playAt)
            return;

        sound.DistortPlayed = true;
        _audio.PlayGlobal(sound.DistortSound, Filter.BroadcastMap(Transform(uid).MapID), true);
    }

    private void HandleSoundLoop(
        EntityUid uid,
        SupermatterComponent supermatter,
        SupermatterStatusType status,
        PirateSupermatterSoundComponent sound,
        AmbientSoundComponent ambient)
    {
        var volume = (float) Math.Round(Math.Clamp(supermatter.Power / 50f - 5f, -5f, 5f));
        _ambient.SetVolume(uid, volume, ambient);

        var desiredLoop = status >= SupermatterStatusType.Danger
            ? sound.DelamLoopSound
            : sound.CalmLoopSound;

        if (ambient.Sound != desiredLoop)
            _ambient.SetSound(uid, desiredLoop, ambient);
    }

    private void HandleAccent(
        EntityUid uid,
        SupermatterComponent supermatter,
        SupermatterStatusType status,
        PirateSupermatterSoundComponent sound)
    {
        if (sound.AccentLastTime >= _timing.CurTime || !_random.Prob(0.05f))
            return;

        var aggression = Math.Min(supermatter.Damage / 800f * (supermatter.Power / 2500f), 1f) * 100f;
        var nextSound = Math.Max(Math.Round((100f - aggression) * 5f), sound.AccentMinCooldown);

        if (sound.AccentLastTime + TimeSpan.FromSeconds(nextSound) > _timing.CurTime)
            return;

        sound.AccentLastTime = _timing.CurTime;
        _audio.PlayPvs(
            status >= SupermatterStatusType.Danger ? sound.DelamAccent : sound.CalmAccent,
            uid);
    }

    private static SupermatterStatusType GetStatus(
        SupermatterComponent supermatter,
        PirateSupermatterSoundComponent sound)
    {
        if (supermatter.Delamming || supermatter.Damage >= supermatter.DelaminationPoint)
            return SupermatterStatusType.Delaminating;

        if (supermatter.Damage >= supermatter.EmergencyPoint)
            return SupermatterStatusType.Emergency;

        if (supermatter.Damage >= sound.DangerPoint)
            return SupermatterStatusType.Danger;

        if (supermatter.Damage >= supermatter.WarningPoint)
            return SupermatterStatusType.Warning;

        if (supermatter.Temperature > Atmospherics.T0C + supermatter.HeatPenaltyThreshold * 0.8f)
            return SupermatterStatusType.Caution;

        return supermatter.Power > 5f
            ? SupermatterStatusType.Normal
            : SupermatterStatusType.Inactive;
    }
}
