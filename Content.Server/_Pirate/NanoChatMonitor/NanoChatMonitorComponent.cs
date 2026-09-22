// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Pirate.NanoChatMonitor;

/// <summary>
///     History stays on the server and is sent to the viewer in pages.
/// </summary>
[RegisterComponent]
public sealed partial class NanoChatMonitorComponent : Component
{
    [DataField]
    public bool Global;

    [ViewVariables]
    public Dictionary<ulong, NanoChatMonitorConversation> Conversations = new();

    [ViewVariables]
    public Dictionary<string, NanoChatMonitorAttachment> Attachments = new();

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

    public string NameA = string.Empty;

    public string? JobA;

    public string NameB = string.Empty;

    public string? JobB;

    public List<NanoChatMonitorStoredEntry> Entries = new();
}

public sealed class NanoChatMonitorStoredEntry
{
    public TimeSpan Timestamp;
    public uint SenderNumber;
    public string SenderName = string.Empty;
    public string? SenderJob;
    public uint RecipientNumber;
    public string RecipientName = string.Empty;
    public string? RecipientJob;
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
