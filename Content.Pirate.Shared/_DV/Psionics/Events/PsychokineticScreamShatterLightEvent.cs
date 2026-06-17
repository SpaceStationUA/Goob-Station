namespace Content.Shared._DV.Psionics.Events;

[ByRefEvent]
public readonly record struct PsychokineticScreamShatterLightEvent(EntityUid Source, float Range, bool LineOfSight, float PenetratingRadius);
