namespace Content.Shared._Pirate.Weapons.Melee.Components;

/// <summary>
/// Marker component that makes <c>ItemMapperClothingVisualizerSystem</c> mirror
/// the entity's <c>ItemMapper</c> layers onto its equipped clothing states.
/// Requires <c>ItemMapperComponent</c> and <c>AppearanceComponent</c> on the same entity.
/// </summary>
[RegisterComponent]
public sealed partial class ItemMapperClothingVisualizerComponent : Component;
