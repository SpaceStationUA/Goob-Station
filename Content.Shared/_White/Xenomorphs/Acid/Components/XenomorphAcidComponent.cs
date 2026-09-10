using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._White.Xenomorphs.Acid.Components;

[RegisterComponent]
public sealed partial class XenomorphAcidComponent : Component
{
    [DataField]
    public EntProtoId AcidId = "XenomorphAcid";

    [DataField]
    public EntProtoId AshPrototype = "Ash";

    /// <summary>
    /// How long acid lasts on items/corpses before they dissolve into ash.
    /// </summary>
    [DataField]
    public TimeSpan AcidLifeTime = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long acid lasts on anchored structures (walls/doors/etc.) while dealing DoT.
    /// </summary>
    [DataField]
    public TimeSpan StructureAcidLifeTime = TimeSpan.FromSeconds(100);

    [DataField]
    public DamageSpecifier DamagePerSecond = new();
}
