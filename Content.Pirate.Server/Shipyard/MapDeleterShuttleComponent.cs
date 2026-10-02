namespace Content.Pirate.Server.Shipyard;

/// <summary>Deletes the temporary shipyard map when its shuttle leaves via FTL.</summary>
[RegisterComponent, Access(typeof(MapDeleterShuttleSystem))]
public sealed partial class MapDeleterShuttleComponent : Component
{
    public bool Enabled;
    public EntityUid ExpectedMap;
}
