// SPDX-FileCopyrightText: 2025 Tyranex <bobthezombie4@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Chat.Prototypes;
using Content.Shared.Speech;
using Content.Shared.StatusIcon;
using Content.Goobstation.Common.Barks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.MalfAI;

/// <summary>
///     The Malf AI voice-modulator state. This mirrors the state exposed by a
///     a voice mask without requiring
///     the AI to wear an item.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MalfAiVoiceModulatorComponent : Component
{
    /// <summary>
    ///     Name shown for this AI in local and radio speech while the modulator is active.
    ///     Null keeps the AI's real entity name.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? VoiceName;

    /// <summary>
    ///     Optional speech-verb override, matching VoiceMaskComponent semantics.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<SpeechVerbPrototype>? SpeechVerb;

    /// <summary>
    ///     Optional speech-sound prototype (e.g. Borg, Parrot, Cluck).
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<SpeechSoundsPrototype>? SpeechSounds;

    /// <summary>
    ///     Whether identity, speech verb, speech sound and accent changes apply.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active = true;

    /// <summary>
    ///     Whether accents are suppressed while the modulator is active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AccentHide = true;

    /// <summary>
    ///     Optional radio job icon override.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<JobIconPrototype>? JobIconProtoId;

    /// <summary>
    ///     Optional radio job tooltip override derived from the selected job icon.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? JobName;

    // Server-side snapshots keep a copied voice independent of subsequent changes to its owner.
    [DataField]
    public bool CopyingVoice;

    [DataField]
    public ProtoId<BarkPrototype>? CopiedBark;

    [DataField]
    public ProtoId<BarkPrototype>? OriginalBark;

    [DataField]
    public bool BarkOverridden;

    public List<MalfVoiceModulatorCrewMember> CrewSnapshot = new();
}
