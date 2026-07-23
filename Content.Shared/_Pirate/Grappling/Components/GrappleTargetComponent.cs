using Content.Shared._Pirate.Grappling.EntitySystems;

namespace Content.Shared._Pirate.Grappling.Components;

/// <summary>
/// Marks this entity as a valid target for grapples.
/// </summary>
[RegisterComponent]
[Access(typeof(SharedGrapplingSystem))]
public sealed partial class GrappleTargetComponent : Component;
