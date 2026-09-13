// SPDX-FileCopyrightText: 2026 Pirate
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.AlertLevel;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._Pirate.AlertLevel;

/// <summary>
/// Sends announcement recordings to clients for per-player sound and volume selection.
/// </summary>
public sealed class PirateAlertLevelAudioSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly Dictionary<string, SoundSpecifier> LegacySounds = new()
    {
        // These are the non-localized sounds used immediately before SpaceStationUA PR #109.
        ["green"] = new SoundPathSpecifier(
            "/Audio/_Pirate/Announcements/Alerts/Legacy/announce.ogg",
            AudioParams.Default.WithVolume(-2f)),
        ["blue"] = new SoundPathSpecifier("/Audio/Misc/bluealert.ogg"),
        ["violet"] = new SoundPathSpecifier("/Audio/Misc/notice1.ogg"),
        ["yellow"] = new SoundPathSpecifier("/Audio/Misc/notice1.ogg"),
        ["red"] = new SoundPathSpecifier("/Audio/Misc/redalert.ogg"),
        ["orange"] = new SoundPathSpecifier("/Audio/_Pirate/Nuclear/Machines/orange.ogg"),
        ["gamma"] = new SoundPathSpecifier("/Audio/Misc/gamma.ogg", AudioParams.Default.WithVolume(-2f)),
        ["delta"] = new SoundPathSpecifier("/Audio/Misc/delta.ogg", AudioParams.Default.WithVolume(-3f)),
        ["epsilon"] = new SoundPathSpecifier("/Audio/Misc/epsilon.ogg", AudioParams.Default.WithVolume(-2f)),
        ["omicron"] = new SoundPathSpecifier("/Audio/_Goobstation/Misc/omicron.ogg"),
        ["white"] = new SoundPathSpecifier("/Audio/_Pirate/Announcements/Alerts/Legacy/code_white.ogg"),
        ["amber"] = new SoundPathSpecifier("/Audio/_Goobstation/Announcements/amberalarm.ogg"),
    };

    public void Play(string level, SoundSpecifier transcribedSound, Filter recipients)
    {
        var transcribed = _audio.ResolveSound(transcribedSound);
        var legacy = LegacySounds.TryGetValue(level, out var legacySound)
            ? _audio.ResolveSound(legacySound)
            : transcribed;

        RaiseNetworkEvent(
            new AlertLevelSoundEvent(
                transcribed,
                legacy,
                transcribedSound.Params,
                legacySound?.Params ?? transcribedSound.Params),
            recipients,
            recordReplay: true);
    }

    public void PlayAnnouncement(SoundSpecifier sound, Filter recipients, AudioParams audioParams)
    {
        RaiseNetworkEvent(
            new AnnouncementSoundEvent(_audio.ResolveSound(sound), audioParams),
            recipients,
            recordReplay: true);
    }
}
