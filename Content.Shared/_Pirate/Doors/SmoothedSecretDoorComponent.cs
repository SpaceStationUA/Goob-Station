using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Doors;

// IconSmooth corner states come from the sprite RSI; the base door layer uses its own RSI.
[RegisterComponent, NetworkedComponent]
public sealed partial class SmoothedSecretDoorComponent : Component;
