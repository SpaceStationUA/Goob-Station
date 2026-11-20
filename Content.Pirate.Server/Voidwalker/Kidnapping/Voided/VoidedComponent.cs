using Content.Pirate.Shared.Voidwalker;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Pirate.Server.Voidwalker.Kidnapping.Voided;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class VoidedComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(15);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ExpireTime;

    /// <summary>
    /// The voidwalker that kidnapped this entity.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Entity<VoidwalkerComponent>? Voidwalker;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextSpacedCheck;

    [DataField]
    public TimeSpan SpacedCheckInterval = TimeSpan.FromSeconds(2);

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextVomitTime;

    [DataField]
    public TimeSpan MinVomitInterval = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan MaxVomitInterval = TimeSpan.FromSeconds(90);

    [DataField]
    public float HungerLost = -10f;

    [DataField]
    public float ThirstLost = -10f;

    [DataField]
    public string NebulaVomitProto = "NebulaVomit";

    [DataField]
    public TimeSpan CleanseTime = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan CleanseTimeChaplain = TimeSpan.FromSeconds(10);

    [DataField]
    public EntProtoId CleanseVFX = "CleanseEffectVFX";

    [DataField]
    public SoundSpecifier CleanseSound = new SoundPathSpecifier("/Audio/_DV/CosmicCult/cleanse_deconversion.ogg");
}
