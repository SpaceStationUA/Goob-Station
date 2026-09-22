// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Pirate.NanoChatMonitor;

/// <summary>
///     History stays on the server and is sent to the viewer in pages.
/// </summary>
/// <remarks>
///     Ordinary telecom servers record covered traffic and redact Syndicate participants. Syndicate
///     relays record all traffic without redaction.
/// </remarks>
[RegisterComponent]
public sealed partial class NanoChatMonitorComponent : Component
{
    [ViewVariables]
    public Dictionary<ulong, NanoChatMonitorConversation> Conversations = new();

    [ViewVariables]
    public Dictionary<string, NanoChatMonitorAttachment> Attachments = new();

    [ViewVariables]
    public Dictionary<ulong, ulong> ClientKeys = new();

    [ViewVariables]
    public Dictionary<ulong, ulong> ConversationKeys = new();

    [ViewVariables]
    public ulong NextClientKey = 1;

    [DataField]
    public TimeSpan PrintCooldown = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public TimeSpan NextPrint;

    [DataField]
    public int MaxLogSheets = 20;
}

public sealed class NanoChatMonitorConversation
{
    public uint NumberA;
    public uint NumberB;

    /// <summary>
    ///     Whether this side was recorded redacted, in which case its identity was never stored and
    ///     everything about it is replaced on the way out to a reader.
    /// </summary>
    public bool RedactedA;

    public bool RedactedB;

    public string NameA = string.Empty;

    public string? JobA;

    public string NameB = string.Empty;

    public string? JobB;

    public List<NanoChatMonitorStoredEntry> Entries = new();
}

public sealed class NanoChatMonitorStoredEntry
{
    /// <summary>
    ///     Identifies the delivery this entry came from. Several machines record the same send, so this
    ///     is what collapses them back into one message when a viewer reads a federated history.
    /// </summary>
    public ulong DeliveryId;

    public TimeSpan Timestamp;
    public uint SenderNumber;
    public string SenderName = string.Empty;
    public string? SenderJob;

    /// <summary>
    ///     Whether the sender was a designated Syndicate endpoint this machine is not allowed to name.
    ///     When set, the identity and location fields were never filled in, and the number is kept only
    ///     as a key: it is replaced with the redaction marker everywhere a reader could see it.
    /// </summary>
    public bool SenderRedacted;

    public uint RecipientNumber;
    public string RecipientName = string.Empty;
    public string? RecipientJob;
    public bool RecipientRedacted;
    /// <summary>
    ///     Blanks content from a Syndicate sender while preserving its length. Ordinary machines never
    ///     store the original message.
    /// </summary>
    public string Content = string.Empty;

    public string Location = string.Empty;

    public string? AttachmentId;

    public string? AttachmentName;
}

public sealed class NanoChatMonitorAttachment
{
    public string FileName = string.Empty;
    public byte[]? ImageData;
    public byte[]? PreviewData;
    public string? Caption;
    public string? Description;

    public List<string> NamesSeen = new();
}
