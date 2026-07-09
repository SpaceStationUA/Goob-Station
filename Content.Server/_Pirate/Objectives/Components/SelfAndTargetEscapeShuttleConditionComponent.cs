using Content.Server._Pirate.Objectives.Systems;

namespace Content.Server._Pirate.Objectives.Components;

[RegisterComponent, Access(typeof(SelfAndTargetEscapeShuttleConditionSystem))]
public sealed partial class SelfAndTargetEscapeShuttleConditionComponent : Component;
