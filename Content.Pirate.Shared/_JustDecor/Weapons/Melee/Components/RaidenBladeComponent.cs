using Robust.Shared.GameStates;

namespace Content.Pirate.Shared._JustDecor.Weapons.Melee;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RaidenBladeComponent : Component
{
    [DataField, AutoNetworkedField]
    public float StaminaHealOnHit = 8f;
}
