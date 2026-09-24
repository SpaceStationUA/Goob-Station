// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.NanoChatMonitor;

[Serializable, NetSerializable]
public enum NanoChatMonitorUiKey : byte
{
    Key,
}

public static class NanoChatMonitorConstants
{
    public const int PageSize = 50;

    public const char RedactedChar = '█';

    /// <summary>
    ///     Stands in for a NanoChat number an ordinary server may not show. Numbers are always printed
    ///     four digits wide, so this is the same width as the real thing.
    /// </summary>
    public const string Redacted = "████";

    /// <summary>
    ///     Blanks out a value an ordinary server is not allowed to reveal, keeping its length so the
    ///     reader can still see the shape of what was said. Applied server side at record time: the
    ///     original is never stored, so there is nothing to leak later.
    /// </summary>
    public static string? Mask(string? value)
    {
        return value is null ? null : new string(RedactedChar, value.Length);
    }
}

/// <remarks>
///     The value of <paramref name="Key" /> is an opaque handle minted by the machine
///     being read, not anything derived from the participants. Numbers are pre-formatted strings rather
///     than integers so a redacted one has nothing left to decode.
/// </remarks>
[Serializable, NetSerializable]
public readonly record struct NanoChatMonitorConversationSummary(
    ulong Key,
    string NumberA,
    string NameA,
    string? JobA,
    string NumberB,
    string NameB,
    string? JobB,
    int MessageCount,
    TimeSpan LastTimestamp,
    ulong Revision);

/// <param name="SenderIsA">
///     Which side of the conversation sent this. Carried explicitly because
///     two redacted participants are indistinguishable by number.
/// </param>
[Serializable, NetSerializable]
public readonly record struct NanoChatMonitorLogEntry(
    TimeSpan Timestamp,
    bool SenderIsA,
    string SenderNumber,
    string SenderName,
    string? SenderJob,
    string RecipientNumber,
    string RecipientName,
    string? RecipientJob,
    string Content,
    string Location,
    string? AttachmentId,
    string? AttachmentName,
    byte[]? AttachmentPreview);

[Serializable, NetSerializable]
public sealed class NanoChatMonitorUiState : BoundUserInterfaceState
{
    public readonly List<NanoChatMonitorConversationSummary> Conversations;

    public NanoChatMonitorUiState(List<NanoChatMonitorConversationSummary> conversations)
    {
        Conversations = conversations;
    }
}

[Serializable, NetSerializable]
public sealed class NanoChatMonitorRequestPageMessage : BoundUserInterfaceMessage
{
    public readonly ulong ConversationKey;

    public readonly int StartIndex;

    public readonly bool Latest;

    public readonly ulong RequestId;

    public NanoChatMonitorRequestPageMessage(ulong conversationKey, int startIndex, bool latest, ulong requestId)
    {
        ConversationKey = conversationKey;
        StartIndex = startIndex;
        Latest = latest;
        RequestId = requestId;
    }
}

[Serializable, NetSerializable]
public sealed class NanoChatMonitorPageMessage : BoundUserInterfaceMessage
{
    public readonly ulong ConversationKey;
    public readonly int StartIndex;
    public readonly int TotalCount;
    public readonly List<NanoChatMonitorLogEntry> Entries;
    public readonly ulong Revision;
    public readonly ulong RequestId;

    public NanoChatMonitorPageMessage(
        ulong conversationKey,
        int startIndex,
        int totalCount,
        List<NanoChatMonitorLogEntry> entries,
        ulong revision,
        ulong requestId = 0)
    {
        ConversationKey = conversationKey;
        StartIndex = startIndex;
        TotalCount = totalCount;
        Entries = entries;
        Revision = revision;
        RequestId = requestId;
    }
}

[Serializable, NetSerializable]
public sealed class NanoChatMonitorRequestAttachmentMessage : BoundUserInterfaceMessage
{
    public readonly string AttachmentId;

    public NanoChatMonitorRequestAttachmentMessage(string attachmentId)
    {
        AttachmentId = attachmentId;
    }
}

[Serializable, NetSerializable]
public sealed class NanoChatMonitorPrintPhotoMessage : BoundUserInterfaceMessage
{
    public readonly string AttachmentId;

    public NanoChatMonitorPrintPhotoMessage(string attachmentId)
    {
        AttachmentId = attachmentId;
    }
}

[Serializable, NetSerializable]
public sealed class NanoChatMonitorPrintLogMessage : BoundUserInterfaceMessage
{
    public readonly ulong ConversationKey;

    public NanoChatMonitorPrintLogMessage(ulong conversationKey)
    {
        ConversationKey = conversationKey;
    }
}

/// <remarks>
///     A cross-network message is stored at both endpoints, so erasing it only on the machine in front
///     of you would leave the other copy standing. This clears the conversation from every server the
///     viewer is federated with. A Syndicate relay federates with nobody, so erasing there never
///     touches the ordinary network, or the other way round.
/// </remarks>
[Serializable, NetSerializable]
public sealed class NanoChatMonitorDeleteLogMessage : BoundUserInterfaceMessage
{
    public readonly ulong ConversationKey;

    public NanoChatMonitorDeleteLogMessage(ulong conversationKey)
    {
        ConversationKey = conversationKey;
    }
}

[Serializable, NetSerializable]
public sealed class NanoChatMonitorAttachmentMessage : BoundUserInterfaceMessage
{
    public readonly string AttachmentId;
    public readonly string FileName;
    public readonly byte[]? ImageData;
    public readonly string? Caption;
    public readonly string? Description;

    public NanoChatMonitorAttachmentMessage(
        string attachmentId,
        string fileName,
        byte[]? imageData,
        string? caption,
        string? description)
    {
        AttachmentId = attachmentId;
        FileName = fileName;
        ImageData = imageData;
        Caption = caption;
        Description = description;
    }
}
