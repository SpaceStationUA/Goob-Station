using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._White.Xenomorphs.Acid.Components;

[RegisterComponent]
public sealed partial class AcidCorrodingComponent : Component
{
    [DataField]
    public DamageSpecifier DamagePerSecond = new();

    [DataField]
    public EntProtoId AshPrototype = "Ash";

    /// <summary>
    /// If true, when the acid expires the target is deleted and replaced with ash (gel style).
    /// If false, only the acid effect is removed (legacy structure corrosion).
    /// </summary>
    [DataField]
    public bool DissolveToAsh = true;

    [ViewVariables]
    public TimeSpan AcidExpiresAt;

    [ViewVariables]
    public TimeSpan NextDamageAt;

    [ViewVariables]
    public EntityUid Acid;
}
