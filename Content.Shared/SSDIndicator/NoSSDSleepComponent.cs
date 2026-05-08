namespace Content.Shared.SSDIndicator;

/// <summary>
///     Marker component to indicate that an entity is an NPC and should not be forced to sleep via SSD. Pirate Code.
///     Ideally this should be moved into a separate server-side system, but this approach is less conflict-prone.
/// </summary>
[RegisterComponent]
public sealed partial class NOSSDSleepComponent : Component
{
}
