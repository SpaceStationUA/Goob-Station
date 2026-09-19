// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.Audio.Jukebox; // Pirate: jukebox records
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Audio.Jukebox;

[NetworkedComponent, RegisterComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedJukeboxSystem), typeof(SharedJukeboxRecordSystem))] // Pirate: jukebox records
public sealed partial class JukeboxComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<JukeboxPrototype>? SelectedSongId;

    // Pirate: jukebox records
    /// <summary>Selected record; mutually exclusive with <see cref="SelectedSongId"/>.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SelectedRecord;
    // End Pirate: jukebox records

    // Pirate: Shuffle & Repeat
    /// <summary>
    /// Whether or not the currently selected song is the first being played.
    /// Useful for shuffle.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool FirstPlay = true;

    [ViewVariables]
    public JukeboxPlaybackMode PlaybackMode = JukeboxPlaybackMode.Single;
    // End Pirate: Shuffle & Repeat

    [DataField, AutoNetworkedField]
    public EntityUid? AudioStream;

    /// <summary>
    /// RSI state for the jukebox being on.
    /// </summary>
    [DataField]
    public string? OnState;

    /// <summary>
    /// RSI state for the jukebox being on.
    /// </summary>
    [DataField]
    public string? OffState;

    /// <summary>
    /// RSI state for the jukebox track being selected.
    /// </summary>
    [DataField]
    public string? SelectState;

    [ViewVariables]
    public bool Selecting;

    [ViewVariables]
    public float SelectAccumulator;
}

[Serializable, NetSerializable]
public sealed class JukeboxPlayingMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class JukeboxPauseMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class JukeboxStopMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class JukeboxSelectedMessage(ProtoId<JukeboxPrototype> songId) : BoundUserInterfaceMessage
{
    public ProtoId<JukeboxPrototype> SongId { get; } = songId;
}

[Serializable, NetSerializable]
public sealed class JukeboxSetTimeMessage(float songTime) : BoundUserInterfaceMessage
{
    public float SongTime { get; } = songTime;
}

// Pirate: Shuffle & Repeat
[Serializable, NetSerializable]
public sealed class JukeboxSetPlaybackModeMessage(JukeboxPlaybackMode playbackMode) : BoundUserInterfaceMessage
{
    public JukeboxPlaybackMode PlaybackMode = playbackMode;
}

[Serializable, NetSerializable]
public enum JukeboxPlaybackMode : byte
{
    Single,
    Shuffle,
    Repeat,
}
// End Pirate: Shuffle & Repeat

[Serializable, NetSerializable]
public enum JukeboxVisuals : byte
{
    VisualState
}

[Serializable, NetSerializable]
public enum JukeboxVisualState : byte
{
    On,
    Off,
    Select,
}

public enum JukeboxVisualLayers : byte
{
    Base
}
