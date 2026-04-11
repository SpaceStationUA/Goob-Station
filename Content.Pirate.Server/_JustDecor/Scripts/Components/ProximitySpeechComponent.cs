using System;

namespace Content.Pirate.Server._JustDecor.Scripts.Components;

[RegisterComponent]
public sealed partial class ProximitySpeechComponent : Component
{
    [DataField("message")]
    public string? Message;

    [DataField("emoteId")]
    public string? EmoteId;

    [DataField("cooldown")]
    public float Cooldown = 5f;

    [DataField("once")]
    public bool Once;

    public TimeSpan NextAllowedSpeak;
    public bool HasTriggered;
}
