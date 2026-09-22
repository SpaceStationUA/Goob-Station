// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Content.Server.Administration.Logs;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Server._Pirate.Photo;
using Content.Shared._DV.CartridgeLoader.Cartridges;
using Content.Shared._Pirate.NanoChat;
using Content.Shared._Pirate.NanoChatMonitor;
using Content.Shared._Pirate.Photo;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Database;
using Content.Shared.Paper;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.NanoChatMonitor;

/// <summary>
///     Capture and storage are server side; the client receives only requested pages and attachments.
/// </summary>
public sealed class NanoChatMonitorSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly PhotoSystem _photo = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly EntProtoId PaperPrototype = "Paper";
    private static readonly EntProtoId PhotoCardPrototype = "PhotoCard";

    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    private static readonly string[] PlaceholderNames = ["grid", "Map Entity"];

    private const int DefaultSheetSize = 10000;

    private const int TruncationReserve = 256;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NanoChatMessageDeliveredEvent>(OnMessageDelivered);

        Subs.BuiEvents<NanoChatMonitorComponent>(NanoChatMonitorUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<NanoChatMonitorRequestPageMessage>(OnRequestPage);
            subs.Event<NanoChatMonitorRequestAttachmentMessage>(OnRequestAttachment);
            subs.Event<NanoChatMonitorPrintPhotoMessage>(OnPrintPhoto);
            subs.Event<NanoChatMonitorPrintLogMessage>(OnPrintLog);
        });
    }

    #region Capture

    private void OnMessageDelivered(ref NanoChatMessageDeliveredEvent args)
    {
        var query = EntityQueryEnumerator<NanoChatMonitorComponent>();
        while (query.MoveNext(out var uid, out var monitor))
        {
            if (!IsPowered(uid))
                continue;

            if (!ShouldRecord((uid, monitor), ref args))
                continue;

            Record((uid, monitor), ref args);
        }
    }

    private bool ShouldRecord(Entity<NanoChatMonitorComponent> monitor, ref NanoChatMessageDeliveredEvent args)
    {
        if (monitor.Comp.Global)
            return true;

        if (_station.GetOwningStation(monitor) is not { } station)
            return false;

        if (args.SenderCard is { } senderCard && _station.GetOwningStation(senderCard) == station)
            return true;

        foreach (var recipient in args.RecipientCards)
        {
            if (_station.GetOwningStation(recipient) == station)
                return true;
        }

        return false;
    }

    private void Record(Entity<NanoChatMonitorComponent> monitor, ref NanoChatMessageDeliveredEvent args)
    {
        var (senderName, senderJob) = ResolveIdentity(args.SenderCard, args.SenderNumber, args.SenderNameOverride);
        var (recipientName, recipientJob) = ResolveIdentity(
            args.RecipientCards.Count > 0 ? args.RecipientCards[0] : null,
            args.RecipientNumber,
            null);

        var entry = new NanoChatMonitorStoredEntry
        {
            Timestamp = args.Message.Timestamp,
            SenderNumber = args.SenderNumber,
            SenderName = senderName,
            SenderJob = senderJob,
            RecipientNumber = args.RecipientNumber,
            RecipientName = recipientName,
            RecipientJob = recipientJob,
            Content = args.Message.Content,
            Location = ResolveLocation(args.SenderDevice ?? args.SenderCard),
        };

        if (args.Message.Photo is { } photo)
        {
            entry.AttachmentId = StoreAttachment(monitor.Comp, photo);
            entry.AttachmentName = photo.FileName;
        }

        var key = GetConversationKey(args.SenderNumber, args.RecipientNumber);
        if (!monitor.Comp.Conversations.TryGetValue(key, out var conversation))
        {
            conversation = new NanoChatMonitorConversation
            {
                NumberA = Math.Min(args.SenderNumber, args.RecipientNumber),
                NumberB = Math.Max(args.SenderNumber, args.RecipientNumber),
            };
            monitor.Comp.Conversations[key] = conversation;
        }

        conversation.Entries.Add(entry);

        SetParticipantIdentity(conversation, args.SenderNumber, senderName, senderJob);
        SetParticipantIdentity(conversation, args.RecipientNumber, recipientName, recipientJob);

        UpdateUi(monitor);
    }

    private static void SetParticipantIdentity(
        NanoChatMonitorConversation conversation,
        uint number,
        string name,
        string? job)
    {
        if (number == conversation.NumberA)
        {
            conversation.NameA = name;
            conversation.JobA = job;
        }

        if (number == conversation.NumberB)
        {
            conversation.NameB = name;
            conversation.JobB = job;
        }
    }

    private static string StoreAttachment(NanoChatMonitorComponent monitor, NanoChatPhotoData photo)
    {
        var id = HashAttachment(photo);
        if (monitor.Attachments.ContainsKey(id))
            return id;

        monitor.Attachments[id] = new NanoChatMonitorAttachment
        {
            FileName = photo.FileName,
            ImageData = photo.ImageData is { Length: > 0 } image ? [.. image] : null,
            PreviewData = photo.PreviewData is { Length: > 0 } preview ? [.. preview] : null,
            Caption = photo.Caption,
            Description = photo.Description,
            NamesSeen = [.. photo.NamesSeen],
        };

        return id;
    }

    private static string HashAttachment(NanoChatPhotoData photo)
    {
        unchecked
        {
            const ulong prime = 1099511628211;

            var hash = 14695981039346656037;

            hash = Feed(photo.ImageData, hash);
            hash = Feed(photo.PreviewData, hash);

            return hash.ToString("x16");

            static ulong Feed(byte[]? data, ulong state)
            {
                foreach (var b in data ?? [])
                {
                    state ^= b;
                    state *= prime;
                }

                state ^= (ulong) (data?.Length ?? 0);
                return state * prime;
            }
        }
    }

    private (string Name, string? Job) ResolveIdentity(EntityUid? card, uint number, string? nameOverride)
    {
        if (card is { } cardUid && !Deleted(cardUid) && TryComp<IdCardComponent>(cardUid, out var idCard))
        {
            var cardName = idCard.FullName;
            if (!string.IsNullOrWhiteSpace(cardName))
                return (cardName, idCard.LocalizedJobTitle);
        }

        if (!string.IsNullOrWhiteSpace(nameOverride))
            return (nameOverride, null);

        return (Loc.GetString("nanochat-monitor-unknown-participant", ("number", $"{number:D4}")), null);
    }

    private string ResolveLocation(EntityUid? device)
    {
        var unknown = Loc.GetString("nanochat-monitor-location-unknown");

        if (device is not { } uid || Deleted(uid))
            return unknown;

        var xform = Transform(uid);

        if (TryGetPlaceName(xform.GridUid, out var gridName))
            return gridName;

        if (TryGetPlaceName(xform.MapUid, out var mapName))
            return mapName;

        return unknown;
    }

    private bool TryGetPlaceName(EntityUid? uid, [NotNullWhen(true)] out string? name)
    {
        name = null;

        if (uid is not { } place || Deleted(place))
            return false;

        var candidate = Name(place);
        if (string.IsNullOrWhiteSpace(candidate) || PlaceholderNames.Contains(candidate))
            return false;

        name = candidate;
        return true;
    }

    public static ulong GetConversationKey(uint a, uint b)
    {
        var low = Math.Min(a, b);
        var high = Math.Max(a, b);
        return ((ulong) low << 32) | high;
    }

    #endregion

    #region Interface

    private void OnUiOpened(Entity<NanoChatMonitorComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!CanView(ent, args.Actor))
        {
            _ui.CloseUi(ent.Owner, NanoChatMonitorUiKey.Key, args.Actor);
            return;
        }

        UpdateUi(ent);
    }

    private void OnRequestPage(Entity<NanoChatMonitorComponent> ent, ref NanoChatMonitorRequestPageMessage args)
    {
        if (!CanRespond(ent, args.Actor))
            return;

        if (!TryGetPage(ent, args.ConversationKey, args.StartIndex, args.Latest, out var page))
            return;

        _ui.ServerSendUiMessage(ent.Owner, NanoChatMonitorUiKey.Key, page, args.Actor);
    }

    private void OnRequestAttachment(
        Entity<NanoChatMonitorComponent> ent,
        ref NanoChatMonitorRequestAttachmentMessage args)
    {
        if (!CanRespond(ent, args.Actor))
            return;

        if (!TryGetAttachment(ent, args.AttachmentId, out var attachment))
            return;

        _ui.ServerSendUiMessage(ent.Owner, NanoChatMonitorUiKey.Key, attachment, args.Actor);
    }

    private void OnPrintPhoto(Entity<NanoChatMonitorComponent> ent, ref NanoChatMonitorPrintPhotoMessage args)
    {
        if (CanRespond(ent, args.Actor))
            TryPrintPhoto(ent, args.AttachmentId, args.Actor);
    }

    private void OnPrintLog(Entity<NanoChatMonitorComponent> ent, ref NanoChatMonitorPrintLogMessage args)
    {
        if (CanRespond(ent, args.Actor))
            TryPrintLog(ent, args.ConversationKey, args.Actor);
    }

    public bool TryPrintPhoto(Entity<NanoChatMonitorComponent> ent, string attachmentId, EntityUid actor)
    {
        if (!TryStartPrint(ent))
            return false;

        if (!ent.Comp.Attachments.TryGetValue(attachmentId, out var attachment) ||
            attachment.ImageData is not { Length: > 0 } imageData)
        {
            return false;
        }

        var card = Spawn(PhotoCardPrototype, Transform(ent).Coordinates);

        if (!TryComp<PhotoCardComponent>(card, out var photoCard))
        {
            QueueDel(card);
            return false;
        }

        var name = Path.GetFileNameWithoutExtension(attachment.FileName);

        var captureData = attachment.NamesSeen.Count > 0
            ? new PhotoCaptureData(0, 0, [], [.. attachment.NamesSeen])
            : null;

        if (!_photo.TrySetPhotoCardData(
                card,
                photoCard,
                imageData,
                attachment.PreviewData,
                customName: string.IsNullOrWhiteSpace(name) ? null : name,
                customDescription: attachment.Description,
                caption: attachment.Caption,
                baseDescription: attachment.Description,
                captureData: captureData))
        {
            QueueDel(card);
            return false;
        }

        FinishPrint(ent, actor, $"printed intercepted NanoChat photo {attachment.FileName}");
        return true;
    }

    public bool TryPrintLog(Entity<NanoChatMonitorComponent> ent, ulong conversationKey, EntityUid actor)
    {
        if (!TryStartPrint(ent))
            return false;

        if (!ent.Comp.Conversations.TryGetValue(conversationKey, out var conversation) ||
            conversation.Entries.Count == 0)
        {
            return false;
        }

        var sheets = BuildLogSheets(ent.Comp, conversation);
        var coordinates = Transform(ent).Coordinates;

        foreach (var sheet in sheets)
        {
            var paper = Spawn(PaperPrototype, coordinates);
            _paper.SetContent(paper, sheet);
        }

        FinishPrint(ent,
            actor,
            $"printed {conversation.Entries.Count} intercepted NanoChat messages between " +
            $"#{conversation.NumberA:D4} and #{conversation.NumberB:D4} across {sheets.Count} sheet(s)");
        return true;
    }

    private List<string> BuildLogSheets(NanoChatMonitorComponent monitor, NanoChatMonitorConversation conversation)
    {
        var sheetSize = PaperContentSize();
        var maxLength = sheetSize * Math.Max(1, monitor.MaxLogSheets);

        var header = string.Join('\n',
            Loc.GetString("nanochat-monitor-print-log-title"),
            Loc.GetString("nanochat-monitor-print-log-participants",
                ("first", $"{conversation.NameA} (#{conversation.NumberA:D4})"),
                ("second", $"{conversation.NameB} (#{conversation.NumberB:D4})")),
            Loc.GetString("nanochat-monitor-print-log-count", ("count", conversation.Entries.Count)));

        var blocks = new List<string>(conversation.Entries.Count);
        foreach (var entry in conversation.Entries)
        {
            blocks.Add(FormatLogLine(entry));
        }

        var budget = maxLength - header.Length - 1 - TruncationReserve;
        var kept = 0;
        for (var i = blocks.Count - 1; i >= 0; i--)
        {
            var cost = blocks[i].Length + 1;
            if (budget - cost < 0)
                break;

            budget -= cost;
            kept++;
        }

        var builder = new StringBuilder();
        builder.Append(header);
        builder.Append('\n');

        if (kept < blocks.Count)
        {
            builder.Append(Loc.GetString("nanochat-monitor-print-log-truncated",
                ("count", blocks.Count - kept)));
            builder.Append('\n');
        }

        for (var i = blocks.Count - kept; i < blocks.Count; i++)
        {
            builder.Append(blocks[i]);
            builder.Append('\n');
        }

        return Split(builder.ToString(), sheetSize);
    }

    private string FormatLogLine(NanoChatMonitorStoredEntry entry)
    {
        var body = entry.Content;

        if (entry.AttachmentName is { } attachment)
        {
            var photo = Loc.GetString("nanochat-monitor-print-log-photo", ("name", attachment));
            body = string.IsNullOrWhiteSpace(body) ? photo : $"{body} {photo}";
        }

        return Loc.GetString("nanochat-monitor-print-log-line",
            ("time", entry.Timestamp.ToString(@"hh\:mm\:ss")),
            ("sender", entry.SenderName),
            ("number", $"{entry.SenderNumber:D4}"),
            ("location", entry.Location),
            ("message", body));
    }

    private static List<string> Split(string text, int size)
    {
        var sheets = new List<string>();

        for (var offset = 0; offset < text.Length; offset += size)
        {
            sheets.Add(text.Substring(offset, Math.Min(size, text.Length - offset)));
        }

        if (sheets.Count == 0)
            sheets.Add(text);

        return sheets;
    }

    private int PaperContentSize()
    {
        if (_proto.TryIndex(PaperPrototype, out var proto) &&
            proto.TryGetComponent<PaperComponent>(out var paper, EntityManager.ComponentFactory))
        {
            return paper.ContentSize;
        }

        return DefaultSheetSize;
    }

    private bool TryStartPrint(Entity<NanoChatMonitorComponent> ent)
    {
        if (_timing.CurTime < ent.Comp.NextPrint)
            return false;

        ent.Comp.NextPrint = _timing.CurTime + ent.Comp.PrintCooldown;
        return true;
    }

    private void FinishPrint(Entity<NanoChatMonitorComponent> ent, EntityUid actor, string log)
    {
        _audio.PlayPvs(PrintSound, ent.Owner);
        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(actor):actor} {log} at {ToPrettyString(ent):tool}");
    }

    public bool TryGetPage(
        Entity<NanoChatMonitorComponent> ent,
        ulong conversationKey,
        int startIndex,
        bool latest,
        [NotNullWhen(true)] out NanoChatMonitorPageMessage? page)
    {
        page = null;

        if (!ent.Comp.Conversations.TryGetValue(conversationKey, out var conversation))
            return false;

        var total = conversation.Entries.Count;
        var pageSize = NanoChatMonitorConstants.PageSize;

        var start = latest ? total - pageSize : startIndex;
        start = Math.Clamp(start, 0, Math.Max(0, total - 1));

        var count = Math.Max(0, Math.Min(pageSize, total - start));
        var entries = new List<NanoChatMonitorLogEntry>(count);

        for (var i = start; i < start + count; i++)
        {
            entries.Add(BuildLogEntry(ent.Comp, conversation.Entries[i]));
        }

        page = new NanoChatMonitorPageMessage(conversationKey, start, total, entries);
        return true;
    }

    public bool TryGetAttachment(
        Entity<NanoChatMonitorComponent> ent,
        string attachmentId,
        [NotNullWhen(true)] out NanoChatMonitorAttachmentMessage? message)
    {
        message = null;

        if (!ent.Comp.Attachments.TryGetValue(attachmentId, out var attachment))
            return false;

        message = new NanoChatMonitorAttachmentMessage(
            attachmentId,
            attachment.FileName,
            attachment.ImageData,
            attachment.Caption,
            attachment.Description);

        return true;
    }

    private static NanoChatMonitorLogEntry BuildLogEntry(
        NanoChatMonitorComponent monitor,
        NanoChatMonitorStoredEntry entry)
    {
        byte[]? preview = null;
        if (entry.AttachmentId is { } attachmentId &&
            monitor.Attachments.TryGetValue(attachmentId, out var attachment) &&
            attachment.PreviewData is { Length: > 0 } previewData)
        {
            preview = previewData;
        }

        return new NanoChatMonitorLogEntry(
            entry.Timestamp,
            entry.SenderNumber,
            entry.SenderName,
            entry.SenderJob,
            entry.RecipientNumber,
            entry.RecipientName,
            entry.RecipientJob,
            entry.Content,
            entry.Location,
            entry.AttachmentId,
            entry.AttachmentName,
            preview);
    }

    private void UpdateUi(Entity<NanoChatMonitorComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, NanoChatMonitorUiKey.Key))
            return;

        var conversations = new List<NanoChatMonitorConversationSummary>(ent.Comp.Conversations.Count);

        foreach (var (key, conversation) in ent.Comp.Conversations)
        {
            if (conversation.Entries.Count == 0)
                continue;

            var last = conversation.Entries[^1];

            conversations.Add(new NanoChatMonitorConversationSummary(
                key,
                conversation.NumberA,
                conversation.NameA,
                conversation.JobA,
                conversation.NumberB,
                conversation.NameB,
                conversation.JobB,
                conversation.Entries.Count,
                last.Timestamp));
        }

        conversations.Sort(static (a, b) => b.LastTimestamp.CompareTo(a.LastTimestamp));

        _ui.SetUiState(ent.Owner, NanoChatMonitorUiKey.Key, new NanoChatMonitorUiState(conversations, ent.Comp.Global));
    }

    public bool CanView(Entity<NanoChatMonitorComponent> ent, EntityUid actor)
    {
        return IsPowered(ent) && _accessReader.IsAllowed(actor, ent);
    }

    private bool CanRespond(Entity<NanoChatMonitorComponent> ent, EntityUid actor)
    {
        if (!_ui.IsUiOpen(ent.Owner, NanoChatMonitorUiKey.Key, actor))
            return false;

        if (CanView(ent, actor))
            return true;

        _ui.CloseUi(ent.Owner, NanoChatMonitorUiKey.Key, actor);
        return false;
    }

    private bool IsPowered(EntityUid uid)
    {
        return !TryComp<ApcPowerReceiverComponent>(uid, out var power) || power.Powered;
    }

    #endregion
}
