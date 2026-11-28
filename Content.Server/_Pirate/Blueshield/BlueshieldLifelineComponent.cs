// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Pirate.Blueshield;

[RegisterComponent]
public sealed partial class BlueshieldLifelineComponent : Component;

/// <summary>Marks a safe arrival point on the Central Command grid.</summary>
[RegisterComponent]
public sealed partial class BlueshieldLifelineArrivalComponent : Component;

/// <summary>Keeps a dead host's evacuation pending after a nuclear disk blocked their LifeLine.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PendingBlueshieldLifelineComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? NextRetry;
}

/// <summary>Command staff evacuated by LifeLine count as absent from the revolution until they leave Central Command.</summary>
[RegisterComponent]
public sealed partial class LifelineEvacuatedCommandComponent : Component;
