// SPDX-FileCopyrightText: 2026 Pirate
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.AlertLevel;

/// <summary>
/// Carries both versions of an alert-level sound so each client can honor its audio preference.
/// </summary>
[Serializable, NetSerializable]
public sealed class AlertLevelSoundEvent : EntityEventArgs
{
    public readonly ResolvedSoundSpecifier TranscribedSpecifier;
    public readonly ResolvedSoundSpecifier LegacySpecifier;
    public readonly AudioParams TranscribedAudioParams;
    public readonly AudioParams LegacyAudioParams;

    public AlertLevelSoundEvent(
        ResolvedSoundSpecifier transcribedSpecifier,
        ResolvedSoundSpecifier legacySpecifier,
        AudioParams transcribedAudioParams,
        AudioParams legacyAudioParams)
    {
        TranscribedSpecifier = transcribedSpecifier;
        LegacySpecifier = legacySpecifier;
        TranscribedAudioParams = transcribedAudioParams;
        LegacyAudioParams = legacyAudioParams;
    }
}

/// <summary>
/// Carries a regular announcement sound so the receiving client can apply its volume preference.
/// </summary>
[Serializable, NetSerializable]
public sealed class AnnouncementSoundEvent : EntityEventArgs
{
    public readonly ResolvedSoundSpecifier Specifier;
    public readonly AudioParams AudioParams;

    public AnnouncementSoundEvent(ResolvedSoundSpecifier specifier, AudioParams audioParams)
    {
        Specifier = specifier;
        AudioParams = audioParams;
    }
}
