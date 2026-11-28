// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Pirate.Blueshield;

[RegisterComponent]
public sealed partial class BlueshieldLifelineComponent : Component;

/// <summary>Marks a safe arrival point on the Central Command grid.</summary>
[RegisterComponent]
public sealed partial class BlueshieldLifelineArrivalComponent : Component;

/// <summary>Retries a dead host's evacuation when the nuclear disk leaves their inventory.</summary>
[RegisterComponent]
public sealed partial class PendingBlueshieldLifelineComponent : Component;

/// <summary>Command staff evacuated by LifeLine count as absent from the revolution until they leave Central Command.</summary>
[RegisterComponent]
public sealed partial class LifelineEvacuatedCommandComponent : Component;
