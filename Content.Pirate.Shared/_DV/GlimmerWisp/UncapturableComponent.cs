namespace Content.Shared._DV.GlimmerWisp;

/// <summary>
/// Prevents this entity from being pulled, grabbed, or inserted into entity storage
/// (lockers, closets, crates, etc). Used on the glimmer wisp so players can't drag
/// it away and stuff it into a closet.
/// </summary>
[RegisterComponent]
public sealed partial class UncapturableComponent : Component;
