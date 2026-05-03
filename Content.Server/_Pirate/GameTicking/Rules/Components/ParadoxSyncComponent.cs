using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent]
public sealed partial class ParadoxSyncComponent : Component
{
    [DataField]
    public EntityUid Target;

    [DataField]
    public EntProtoId EffectProto = "MobParadoxTimed";
}