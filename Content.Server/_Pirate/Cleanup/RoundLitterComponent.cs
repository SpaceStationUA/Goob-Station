// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Pirate.Cleanup;

/// <summary>Tracks the lifetime of a residue produced during play. Never saved into a map.</summary>
[RegisterComponent, UnsavedComponent, AutoGenerateComponentPause]
public sealed partial class RoundLitterComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ExpiresAt;

    [DataField]
    public TimeSpan Lifetime;

    [DataField]
    public bool FloorOnly;
}
