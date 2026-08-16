namespace Content.Pirate.Shared.Psionics;

/// <summary>
/// Raised by base chat so Pirate-side psionics can handle telepathic messages
/// without Content.Server depending on Content.Pirate.Server.
/// </summary>
public sealed class SendTelepathicChatEvent(EntityUid source, string message, string senderName, bool hideChat) : EntityEventArgs
{
    public EntityUid Source { get; } = source;
    public string Message { get; } = message;
    public string SenderName { get; } = senderName;
    public bool HideChat { get; } = hideChat;
}

[ByRefEvent]
public record struct GetTelepathicChatPermissionsEvent
{
    public bool CanUse;
}
