using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Witch.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class AntiMagicWardComponent : Component
{
    [DataField]
    public ProtoId<DamageModifierSetPrototype> Modifier = "AntiMagicWardProtection";
}
