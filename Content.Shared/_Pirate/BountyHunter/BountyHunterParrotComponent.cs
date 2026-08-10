namespace Content.Shared._Pirate.BountyHunter;

/// <summary>
/// Component to prevent the parrot from attacking and throwing items.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(BountyHunterParrotSystem))]
public sealed partial class BountyHunterParrotComponent : Component
{
}
