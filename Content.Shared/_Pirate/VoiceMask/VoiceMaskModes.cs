// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.VoiceMask;

[Serializable, NetSerializable]
public enum SpeakerNameContext : byte
{
    // Relays without mode metadata use the Say setting.
    Unknown,
    Say,
    Whisper,
    Radio,
}

[Serializable, NetSerializable]
// A null mode state denotes an owner without per-mode support.
public sealed class VoiceMaskModesState
{
    public readonly bool Say;
    public readonly bool Whisper;

    public readonly List<VoiceMaskChannelMode> Channels;

    public VoiceMaskModesState(bool say, bool whisper, List<VoiceMaskChannelMode> channels)
    {
        Say = say;
        Whisper = whisper;
        Channels = channels;
    }
}

[Serializable, NetSerializable]
public readonly record struct VoiceMaskChannelMode(ProtoId<RadioChannelPrototype> Channel, bool Enabled);

[Serializable, NetSerializable]
public sealed class VoiceMaskSetModeMessage : BoundUserInterfaceMessage
{
    public readonly SpeakerNameContext Mode;
    public readonly ProtoId<RadioChannelPrototype>? Channel;
    public readonly bool Enabled;

    public VoiceMaskSetModeMessage(SpeakerNameContext mode, ProtoId<RadioChannelPrototype>? channel, bool enabled)
    {
        Mode = mode;
        Channel = channel;
        Enabled = enabled;
    }
}
