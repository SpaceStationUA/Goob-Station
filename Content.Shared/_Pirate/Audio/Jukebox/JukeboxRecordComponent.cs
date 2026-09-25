// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Audio.Jukebox;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class JukeboxRecordComponent : Component
{
    /// <summary>Rooted audio resource path; null leaves the record blank.</summary>
    [DataField, AutoNetworkedField]
    public string? Path;

    /// <summary>Track name shown in the UI and on examine.</summary>
    [DataField, AutoNetworkedField]
    public string? Title;
}
