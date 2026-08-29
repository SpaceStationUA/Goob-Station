// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.SubFloor;

[RegisterComponent, NetworkedComponent]
public sealed partial class TrayScannerComponent : Component
{
    /// <summary>
    ///     Whether the scanner is currently on.
    /// </summary>
    [DataField]
    public bool Enabled;

    /// <summary>
    ///     Radius in which the scanner will reveal entities. Centered on the <see cref="LastLocation"/>.
    /// </summary>
    [DataField]
    public float Range = 4f;

    // Pirate: meson vision - ported from Moffstation PR #1688 (toggle action + on/off sounds for worn scanners).

    /// <summary>
    /// The action prototype to give to the user when equipped.
    /// </summary>
    [DataField]
    public EntProtoId? ToggleAction;

    /// <summary>
    /// The spawned action entity linked to this scanner.
    /// </summary>
    [DataField, NonSerialized]
    public EntityUid? ToggleActionEntity;

    /// <summary>
    /// Sound played when the scanner is turned on.
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundOn;

    /// <summary>
    /// Sound played when the scanner is turned off.
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundOff;
}

[Serializable, NetSerializable]
public sealed class TrayScannerState : ComponentState
{
    public bool Enabled;
    public float Range;

    public TrayScannerState(bool enabled, float range)
    {
        Enabled = enabled;
        Range = range;
    }
}