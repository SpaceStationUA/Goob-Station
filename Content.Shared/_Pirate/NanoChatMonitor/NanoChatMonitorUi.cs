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
}

[Serializable, NetSerializable]
public readonly record struct NanoChatMonitorConversationSummary(
    ulong Key,
    uint NumberA,
    string NameA,
    string? JobA,
    uint NumberB,
    string NameB,
    string? JobB,
    int MessageCount,
    TimeSpan LastTimestamp);

[Serializable, NetSerializable]
public readonly record struct NanoChatMonitorLogEntry(
    TimeSpan Timestamp,
    uint SenderNumber,
    string SenderName,
    string? SenderJob,
    uint RecipientNumber,
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

    public readonly bool Global;

    public NanoChatMonitorUiState(List<NanoChatMonitorConversationSummary> conversations, bool global)
    {
        Conversations = conversations;
        Global = global;
    }
}

[Serializable, NetSerializable]
public sealed class NanoChatMonitorRequestPageMessage : BoundUserInterfaceMessage
{
    public readonly ulong ConversationKey;

    public readonly int StartIndex;

    public readonly bool Latest;

    public NanoChatMonitorRequestPageMessage(ulong conversationKey, int startIndex, bool latest)
    {
        ConversationKey = conversationKey;
        StartIndex = startIndex;
        Latest = latest;
    }
}

[Serializable, NetSerializable]
public sealed class NanoChatMonitorPageMessage : BoundUserInterfaceMessage
{
    public readonly ulong ConversationKey;
    public readonly int StartIndex;
    public readonly int TotalCount;
    public readonly List<NanoChatMonitorLogEntry> Entries;

    public NanoChatMonitorPageMessage(
        ulong conversationKey,
        int startIndex,
        int totalCount,
        List<NanoChatMonitorLogEntry> entries)
    {
        ConversationKey = conversationKey;
        StartIndex = startIndex;
        TotalCount = totalCount;
        Entries = entries;
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
