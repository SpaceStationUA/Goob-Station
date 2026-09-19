// SPDX-FileCopyrightText: 2026 Pirate
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.AlertLevel;
using Content.Shared._Pirate.CCVars;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._Pirate.AlertLevel;

/// <summary>
/// Plays announcement recordings using this client's audio settings.
/// </summary>
public sealed class PirateAlertLevelAudioSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<AlertLevelSoundEvent>(OnAlertLevelSound);
        SubscribeNetworkEvent<AnnouncementSoundEvent>(OnAnnouncementSound);
    }

    private void OnAlertLevelSound(AlertLevelSoundEvent ev)
    {
        var transcribed = _cfg.GetCVar(PirateVars.TranscribedAnnouncementSounds);
        var specifier = transcribed ? ev.TranscribedSpecifier : ev.LegacySpecifier;
        var audioParams = transcribed ? ev.TranscribedAudioParams : ev.LegacyAudioParams;

        Play(specifier, audioParams);
    }

    private void OnAnnouncementSound(AnnouncementSoundEvent ev)
    {
        Play(ev.Specifier, ev.AudioParams);
    }

    private void Play(ResolvedSoundSpecifier specifier, AudioParams audioParams)
    {
        var volume = SharedAudioSystem.GainToVolume(_cfg.GetCVar(PirateVars.AnnouncementVolume));

        _audio.PlayGlobal(specifier, Filter.Local(), false, audioParams.AddVolume(volume));
    }
}
