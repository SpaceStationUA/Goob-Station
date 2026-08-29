// SPDX-License-Identifier: MIT

using Robust.Shared.Audio; // Pirate: meson vision
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes; // Pirate: meson vision
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

    [DataField] // Pirate: meson vision
    public bool ToggleOnActivate = true; // Pirate: meson vision

    /// <summary>
    ///     Radius in which the scanner will reveal entities. Centered on the <see cref="LastLocation"/>.
    /// </summary>
    [DataField]
    public float Range = 4f;

    // Pirate: meson vision - ported from Moffstation PR #1688 (funky-station/forky-station#102).

    #region Pirate: meson vision
    [DataField]
    public EntProtoId? ToggleAction;

    [DataField, NonSerialized]
    public EntityUid? ToggleActionEntity;

    [DataField]
    public SoundSpecifier? SoundOn;

    [DataField]
    public SoundSpecifier? SoundOff;
    #endregion Pirate: meson vision
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
