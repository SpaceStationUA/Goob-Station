using Content.Shared.Damage;

namespace Content.Pirate.Shared.ModularSuit;

[RegisterComponent]
public sealed partial class AffectedModuleArmorBoosterComponent : Component
{
    [DataField(required: true)]
    public DamageModifierSet Modifiers = new();
}
