namespace Content.Pirate.Server._JustDecor.Scripts.Components;

[RegisterComponent]
public sealed partial class LinkedTeleportTargetComponent : Component
{
    [DataField("linkId", required: true)]
    public string LinkId = string.Empty;
}
