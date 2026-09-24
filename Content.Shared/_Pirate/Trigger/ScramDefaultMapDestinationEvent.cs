using Robust.Shared.Map;

namespace Content.Shared._Pirate.Trigger;

[ByRefEvent]
public record struct ScramDefaultMapDestinationEvent(EntityUid Target)
{
    public EntityCoordinates? Coordinates;
}
