// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Pirate.Shared.Skia;

/// <summary>
/// Tracks the ambient light level used by Skia's health mechanics.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedSkiaLightReactiveSystem))]
public sealed partial class SkiaLightReactiveComponent : Component
{
    [DataField]
    public TimeSpan UpdateFrequency = TimeSpan.FromSeconds(1);

    [DataField]
    public bool OnlyWhileAlive = true;

    [DataField]
    public bool Manual;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [DataField]
    public float CurrentLightLevel;
}
