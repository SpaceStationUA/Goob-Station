// SPDX-FileCopyrightText: 2026 Pirate
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.AlertLevel;
using Content.Shared._Pirate.CCVars;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._Pirate.AlertLevel;

/// <summary>
/// Plays the alert-level recording selected in this client's audio settings.
/// </summary>
public sealed class PirateAlertLevelAudioSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<AlertLevelSoundEvent>(OnAlertLevelSound);
    }

    private void OnAlertLevelSound(AlertLevelSoundEvent ev)
    {
        var transcribed = _cfg.GetCVar(PirateVars.TranscribedAnnouncementSounds);
        var specifier = transcribed ? ev.TranscribedSpecifier : ev.LegacySpecifier;
        var audioParams = transcribed ? ev.TranscribedAudioParams : ev.LegacyAudioParams;

        _audio.PlayGlobal(specifier, Filter.Local(), false, audioParams);
    }
}
