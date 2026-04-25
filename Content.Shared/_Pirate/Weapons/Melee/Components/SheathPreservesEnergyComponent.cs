namespace Content.Shared._Pirate.Weapons.Melee.Components;

/// <summary>
/// Marks a storage/sheath entity as preserving the energy of a <see cref="TimedDeflectBlockComponent"/>
/// weapon stored inside it — decay runs at <see cref="TimedDeflectBlockComponent.SheathDecayMultiplier"/> speed.
/// </summary>
[RegisterComponent]
public sealed partial class SheathPreservesEnergyComponent : Component;
