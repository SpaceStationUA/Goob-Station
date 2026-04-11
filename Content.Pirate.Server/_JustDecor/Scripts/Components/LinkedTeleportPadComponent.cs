using System;

namespace Content.Pirate.Server._JustDecor.Scripts.Components;

[RegisterComponent]
public sealed partial class LinkedTeleportPadComponent : Component
{
    [DataField("linkId", required: true)]
    public string LinkId = string.Empty;

    [DataField("cooldown")]
    public float Cooldown = 0.25f;

    [DataField("relinkInterval")]
    public float RelinkInterval = 1f;

    public EntityUid? Target;
    public TimeSpan NextUse;
    public TimeSpan NextRelink;
}
