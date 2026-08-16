using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Pirate.Shared.ModularSuit;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), Access(typeof(SharedModularSuitSystem))]
public sealed partial class ModularSuitCoreComponent : Component
{
    [DataField]
    public float MaxCharge = 100f;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public float Charge = 100f;

    [DataField]
    public float ChargeRate = 10f;

    [DataField("multiplier")]
    public float DrawMultiplier = 1.0f;

    [DataField]
    public bool Infinite;

    [DataField]
    public SpriteSpecifier? VerbIcon;
}
