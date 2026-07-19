using Robust.Shared.GameStates;

namespace Content.Shared.Materials.OreSilo;

/// <summary>
/// An entity with <see cref="MaterialStorageComponent"/> that interfaces with an <see cref="OreSiloComponent"/>.
/// Used for tracking the connected silo.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedOreSiloSystem))]
public sealed partial class OreSiloClientComponent : Component
{
    /// <summary>
    /// The silo that this client pulls materials from.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Silo;

    /// <summary>Map key for auto-linking this client to a silo in the same z-network.</summary>
    [DataField]
    public string? SiloNetwork; // Pirate: multiz
}
