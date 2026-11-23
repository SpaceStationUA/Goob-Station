// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Vehicles;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class StvComponent : Component
{
    [AutoPausedField] public TimeSpan? IgnitionAt;
    [AutoPausedField] public TimeSpan NextFuelDraw;
    public EntityUid? IgnitionAction;
    public EntityUid? HeadlightAction;
    public EntityUid? SirenAction;
}

public enum StvControl
{
    Ignition,
    Headlight,
    Siren,
}

public sealed partial class StvControlActionEvent : InstantActionEvent
{
    [DataField(required: true)] public StvControl Control;
}

[Serializable, NetSerializable]
public enum StvVisuals
{
    Broken,
}
