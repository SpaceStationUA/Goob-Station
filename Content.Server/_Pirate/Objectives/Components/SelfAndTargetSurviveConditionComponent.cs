using Content.Server._Pirate.Objectives.Systems;

namespace Content.Server._Pirate.Objectives.Components;

[RegisterComponent, Access(typeof(SelfAndTargetSurviveConditionSystem))]
public sealed partial class SelfAndTargetSurviveConditionComponent : Component;
