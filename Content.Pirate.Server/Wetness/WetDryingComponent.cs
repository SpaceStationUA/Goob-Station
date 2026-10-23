namespace Content.Pirate.Server.Wetness;

// Persist this marker because saved wet items may load without MapInitEvent.
[RegisterComponent]
public sealed partial class WetDryingComponent : Component;
