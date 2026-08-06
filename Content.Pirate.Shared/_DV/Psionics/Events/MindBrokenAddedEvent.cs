namespace Content.Shared._DV.Psionics.Events;

/// <summary>
///     Raised after an entity gains <see cref="Content.Shared._DV.Psionics.Components.MindBrokenComponent"/>
///     and the initial mindbroken side effects (insulation, assay response, component stripping) were applied.
/// </summary>
[ByRefEvent]
public record struct MindBrokenAddedEvent();
