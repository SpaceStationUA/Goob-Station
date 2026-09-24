// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Content.Server.Administration.Logs;
using Content.Server.Power.Components;
using Content.Server._Pirate.NanoChat;
using Content.Server._Pirate.Photo;
using Content.Shared._DV.CartridgeLoader.Cartridges;
using Content.Shared._Pirate.NanoChat;
using Content.Shared._Pirate.NanoChatMonitor;
using Content.Shared._Pirate.Photo;
using Content.Shared.Access.Components;
using Content.Shared.Database;
using Content.Shared.Paper;
using Content.Shared.Power;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.NanoChatMonitor;

/// <summary>
///     Capture and storage are server-side; the client receives only requested pages and attachments.
/// </summary>
/// <remarks>
///     A viewer sees logs from every powered machine connected to its own; ordinary servers store
///     Syndicate identities as redacted data.
/// </remarks>
public sealed class NanoChatMonitorSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly NanoChatNetworkSystem _network = default!;
    [Dependency] private readonly SharedNanoChatLogHostSystem _logHost = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly PhotoSystem _photo = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly EntProtoId PaperPrototype = "Paper";
    private static readonly EntProtoId PhotoCardPrototype = "PhotoCard";

    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    private static readonly string[] PlaceholderNames = ["grid", "Map Entity"];

    private const int DefaultSheetSize = 10000;

    private const int TruncationReserve = 256;

    /// <summary>
    ///     Reusable list for federated server lookups.
    /// </summary>
    private readonly List<EntityUid> _servers = new();

    private readonly Dictionary<EntityUid, Dictionary<ulong, MergedConversation>> _mergeCache = new();

    // Keep revisions across merge-cache invalidations so append-only updates preserve loaded pages.
    private readonly Dictionary<EntityUid, Dictionary<ulong, RevisionSnapshot>> _revisions = new();

    private readonly record struct RevisionSnapshot(int Count, ulong Hash, ulong Revision);

    private void InvalidateMergeCache()
    {
        _mergeCache.Clear();
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NanoChatMessageDeliveredEvent>(OnMessageDelivered);

        SubscribeLocalEvent<NanoChatMonitorComponent, PowerChangedEvent>(OnMonitorPowerChanged);
        SubscribeLocalEvent<NanoChatNetworkChangedEvent>(OnNetworkChanged);

        SubscribeLocalEvent<NanoChatMonitorComponent, EncryptionChannelsChangedEvent>(OnKeysChanged);

        SubscribeLocalEvent<NanoChatMonitorComponent, ComponentShutdown>(OnMonitorRemoved);
        SubscribeLocalEvent<NanoChatMonitorComponent, EntParentChangedMessage>(OnMonitorMoved);

        Subs.BuiEvents<NanoChatMonitorComponent>(NanoChatMonitorUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<NanoChatMonitorRequestPageMessage>(OnRequestPage);
            subs.Event<NanoChatMonitorRequestAttachmentMessage>(OnRequestAttachment);
            subs.Event<NanoChatMonitorPrintPhotoMessage>(OnPrintPhoto);
            subs.Event<NanoChatMonitorPrintLogMessage>(OnPrintLog);
            subs.Event<NanoChatMonitorDeleteLogMessage>(OnDeleteLog);
        });
    }

    #region Capture

    private void OnMessageDelivered(ref NanoChatMessageDeliveredEvent args)
    {
        var topology = _network.BuildTopology();
        var recording = new List<EntityUid>();
        _network.GetRecordingServers(topology, args.SenderDevice ?? args.SenderCard, args.RecipientCards, recording);

        if (recording.Count == 0)
            return;

        var senderSyndicate = _network.IsSyndicateCard(args.SenderCard) ||
                              _network.IsSyndicateDevice(args.SenderDevice);

        var recipientSyndicate = args.RecipientCards.Any(card => _network.IsSyndicateCard(card));

        foreach (var uid in recording)
        {
            if (!TryComp<NanoChatMonitorComponent>(uid, out var monitor))
                continue;

            var redact = !IsRelay(uid);

            Record((uid, monitor), ref args, senderSyndicate && redact, recipientSyndicate && redact);
        }

        InvalidateMergeCache();
        RefreshOpenViewers();
    }

    private void Record(
        Entity<NanoChatMonitorComponent> monitor,
        ref NanoChatMessageDeliveredEvent args,
        bool redactSender,
        bool redactRecipient)
    {
        // Everything a redacted participant would give away is blanked here,
        // at record time, keeping only its length. The real value is never written down, so the only
        // thing left to withhold on the way out is the number, which is kept as the conversation key.
        var (senderName, senderJob) = ResolveIdentity(args.SenderCard, args.SenderNumber, args.SenderNameOverride);
        var senderLocation = ResolveLocation(args.SenderDevice ?? args.SenderCard);
        var content = args.Message.Content;

        if (redactSender)
        {
            senderName = NanoChatMonitorConstants.Mask(senderName)!;
            senderJob = NanoChatMonitorConstants.Mask(senderJob);
            senderLocation = NanoChatMonitorConstants.Mask(senderLocation)!;

            content = NanoChatMonitorConstants.Mask(content)!;
        }

        var (recipientName, recipientJob) = ResolveIdentity(
            args.RecipientCards.Count > 0 ? args.RecipientCards[0] : null,
            args.RecipientNumber,
            null);

        if (redactRecipient)
        {
            recipientName = NanoChatMonitorConstants.Mask(recipientName)!;
            recipientJob = NanoChatMonitorConstants.Mask(recipientJob);
        }

        var entry = new NanoChatMonitorStoredEntry
        {
            DeliveryId = args.DeliveryId,
            Timestamp = args.Message.Timestamp,
            SenderNumber = args.SenderNumber,
            SenderName = senderName,
            SenderJob = senderJob,
            SenderRedacted = redactSender,
            RecipientNumber = args.RecipientNumber,
            RecipientName = recipientName,
            RecipientJob = recipientJob,
            RecipientRedacted = redactRecipient,
            Content = content,
            Location = senderLocation,
        };

        if (args.Message.Photo is { } photo)
        {
            if (redactSender)
            {
                entry.AttachmentName = NanoChatMonitorConstants.Mask(photo.FileName);
            }
            else
            {
                entry.AttachmentId = StoreAttachment(monitor.Comp, photo);
                entry.AttachmentName = photo.FileName;
            }
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

        SetParticipantIdentity(conversation, args.SenderNumber, senderName, senderJob, redactSender);
        SetParticipantIdentity(conversation, args.RecipientNumber, recipientName, recipientJob, redactRecipient);
    }

    private static void SetParticipantIdentity(
        NanoChatMonitorConversation conversation,
        uint number,
        string name,
        string? job,
        bool redacted)
    {
        if (number == conversation.NumberA)
        {
            // Redaction only ever latches on, so a card that spent part of
            // the round in a Syndicate PDA cannot be named by moving it back out of one. Names arrive
            // already masked when redacted, so the latch is what stops a later plain one overwriting it.
            conversation.RedactedA |= redacted;

            if (redacted || !conversation.RedactedA)
            {
                conversation.NameA = name;
                conversation.JobA = job;
            }
        }

        if (number == conversation.NumberB)
        {
            conversation.RedactedB |= redacted;

            if (redacted || !conversation.RedactedB)
            {
                conversation.NameB = name;
                conversation.JobB = job;
            }
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

        var location = unknown;
        if (TryGetPlaceName(xform.GridUid, out var gridName))
            location = gridName;
        else if (TryGetPlaceName(xform.MapUid, out var mapName))
            location = mapName;

        var mapCoordinates = _transform.GetMapCoordinates(uid, xform);
        var position = xform.GridUid is { } grid
            ? _transform.ToCoordinates(grid, mapCoordinates).Position
            : mapCoordinates.Position;

        return $"{location} ({(int) MathF.Floor(position.X)}, {(int) MathF.Floor(position.Y)})";
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

    #region Federation

    /// <summary>
    ///     One conversation as a reader sees it: every networked machine's copy folded together, with
    ///     duplicate recordings of the same delivery collapsed back into one message.
    /// </summary>
    private sealed class MergedConversation
    {
        public ulong ClientKey;
        public uint NumberA;
        public uint NumberB;
        public bool RedactedA;
        public bool RedactedB;
        public ulong Revision;
        public string NameA = string.Empty;
        public string? JobA;
        public string NameB = string.Empty;
        public string? JobB;

        public readonly List<(NanoChatMonitorStoredEntry Entry, NanoChatMonitorComponent Source)> Entries = new();

        public readonly HashSet<ulong> Seen = new();

        public TimeSpan IdentityAsOf = TimeSpan.MinValue;
    }

    /// <summary>
    ///     Folds connected machines' histories into conversations, collapsing duplicate deliveries.
    /// </summary>
    private Dictionary<ulong, MergedConversation> BuildMerged(Entity<NanoChatMonitorComponent> viewer)
    {
        if (_mergeCache.TryGetValue(viewer.Owner, out var cached))
            return cached;

        _network.GetFederatedServers(viewer, _servers);

        var alone = _servers.Count <= 1;

        var merged = new Dictionary<ulong, MergedConversation>();
        if (!_revisions.TryGetValue(viewer.Owner, out var revisions))
            _revisions[viewer.Owner] = revisions = new Dictionary<ulong, RevisionSnapshot>();

        foreach (var uid in _servers)
        {
            if (!TryComp<NanoChatMonitorComponent>(uid, out var source))
                continue;

            foreach (var (key, conversation) in source.Conversations)
            {
                if (conversation.Entries.Count == 0)
                    continue;

                if (!merged.TryGetValue(key, out var target))
                {
                    merged[key] = target = new MergedConversation
                    {
                        ClientKey = ResolveClientKey(viewer.Comp, key),
                        NumberA = conversation.NumberA,
                        NumberB = conversation.NumberB,
                        RedactedA = conversation.RedactedA,
                        RedactedB = conversation.RedactedB,
                    };
                }

                target.RedactedA |= conversation.RedactedA;
                target.RedactedB |= conversation.RedactedB;

                foreach (var entry in conversation.Entries)
                {
                    if (!alone && entry.DeliveryId != 0 && !target.Seen.Add(entry.DeliveryId))
                        continue;

                    target.Entries.Add((entry, source));
                }

                var last = conversation.Entries[^1].Timestamp;
                if (last < target.IdentityAsOf)
                    continue;

                target.IdentityAsOf = last;
                target.NameA = conversation.NameA;
                target.JobA = conversation.JobA;
                target.NameB = conversation.NameB;
                target.JobB = conversation.JobB;
            }
        }

        foreach (var conversation in merged.Values)
        {
            if (conversation.RedactedA)
            {
                conversation.NameA = NanoChatMonitorConstants.Mask(conversation.NameA)!;
                conversation.JobA = NanoChatMonitorConstants.Mask(conversation.JobA);
            }

            if (conversation.RedactedB)
            {
                conversation.NameB = NanoChatMonitorConstants.Mask(conversation.NameB)!;
                conversation.JobB = NanoChatMonitorConstants.Mask(conversation.JobB);
            }

            if (!alone)
                conversation.Entries.Sort(static (a, b) => a.Entry.Timestamp.CompareTo(b.Entry.Timestamp));

            var key = GetConversationKey(conversation.NumberA, conversation.NumberB);
            var hash = ComputeRevision(conversation, conversation.Entries.Count);
            if (revisions.TryGetValue(key, out var previous))
            {
                var appendedOnly = conversation.Entries.Count >= previous.Count &&
                                   ComputeRevision(conversation, previous.Count) == previous.Hash;
                conversation.Revision = appendedOnly ? previous.Revision : previous.Revision + 1;
            }
            else
            {
                conversation.Revision = 1;
            }

            revisions[key] = new RevisionSnapshot(conversation.Entries.Count, hash, conversation.Revision);
        }

        _mergeCache[viewer.Owner] = merged;
        return merged;
    }

    private static ulong ComputeRevision(MergedConversation conversation, int count)
    {
        unchecked
        {
            const ulong prime = 1099511628211;
            var hash = 14695981039346656037UL;

            hash = (hash ^ (ulong) count) * prime;
            hash = (hash ^ (conversation.RedactedA ? 1UL : 0UL)) * prime;
            hash = (hash ^ (conversation.RedactedB ? 1UL : 0UL)) * prime;

            for (var i = 0; i < count; i++)
            {
                var entry = conversation.Entries[i].Entry;
                hash = (hash ^ entry.DeliveryId) * prime;
                hash = (hash ^ (ulong) entry.Timestamp.Ticks) * prime;
                hash = (hash ^ (uint) entry.Content.GetHashCode()) * prime;
                hash = (hash ^ (uint) entry.SenderName.GetHashCode()) * prime;
                hash = (hash ^ (uint) (entry.SenderJob?.GetHashCode() ?? 0)) * prime;
                hash = (hash ^ (uint) entry.RecipientName.GetHashCode()) * prime;
                hash = (hash ^ (uint) (entry.RecipientJob?.GetHashCode() ?? 0)) * prime;
                hash = (hash ^ (uint) entry.Location.GetHashCode()) * prime;
                hash = (hash ^ (entry.SenderRedacted ? 1UL : 0UL)) * prime;
                hash = (hash ^ (entry.RecipientRedacted ? 1UL : 0UL)) * prime;
                hash = (hash ^ (uint) (entry.AttachmentId?.GetHashCode() ?? 0)) * prime;
                hash = (hash ^ (uint) (entry.AttachmentName?.GetHashCode() ?? 0)) * prime;
            }

            return hash;
        }
    }

    /// <summary>
    ///     Hands out this machine's opaque handle for a conversation, minting one on first sight. A real
    ///     conversation key is the two participants' numbers packed together, so it can never go on the
    ///     wire: it would give a redacted number straight back to the reader.
    /// </summary>
    private static ulong ResolveClientKey(NanoChatMonitorComponent monitor, ulong conversationKey)
    {
        if (monitor.ClientKeys.TryGetValue(conversationKey, out var existing))
            return existing;

        var handle = monitor.NextClientKey++;
        monitor.ClientKeys[conversationKey] = handle;
        monitor.ConversationKeys[handle] = conversationKey;

        return handle;
    }

    public ulong GetClientKey(Entity<NanoChatMonitorComponent> ent, ulong conversationKey)
    {
        return ResolveClientKey(ent.Comp, conversationKey);
    }


    public bool KeepsLog(EntityUid uid)
    {
        return _logHost.HostsLog(uid);
    }

    private void OnKeysChanged(EntityUid uid, NanoChatMonitorComponent monitor, EncryptionChannelsChangedEvent args)
    {
        if (!KeepsLog(uid))
        {
            ClearLog(monitor);
            _ui.CloseUi(uid, NanoChatMonitorUiKey.Key);
        }

        InvalidateMergeCache();
        RefreshOpenViewers();
    }

    private void OnDeleteLog(Entity<NanoChatMonitorComponent> ent, ref NanoChatMonitorDeleteLogMessage args)
    {
        if (CanRespond(ent, args.Actor))
            DeleteConversation(ent, args.ConversationKey, args.Actor);
    }

    public bool DeleteConversation(Entity<NanoChatMonitorComponent> ent, ulong clientKey, EntityUid actor)
    {
        if (!ent.Comp.ConversationKeys.TryGetValue(clientKey, out var conversationKey))
            return false;

        _network.GetFederatedServers(ent, _servers);

        var erased = 0;
        foreach (var uid in _servers)
        {
            if (!TryComp<NanoChatMonitorComponent>(uid, out var monitor))
                continue;

            if (!monitor.Conversations.Remove(conversationKey, out var conversation))
                continue;

            erased += conversation.Entries.Count;

            if (monitor.ClientKeys.Remove(conversationKey, out var handle))
                monitor.ConversationKeys.Remove(handle);

            PruneAttachments(monitor);
        }

        _adminLogger.Add(LogType.Action,
            LogImpact.High,
            $"{ToPrettyString(actor):actor} erased a NanoChat conversation of {erased} recorded message(s) from {_servers.Count} server(s) at {ToPrettyString(ent):tool}");

        InvalidateMergeCache();
        RefreshOpenViewers();
        return true;
    }

    private static void ClearLog(NanoChatMonitorComponent monitor)
    {
        monitor.Conversations.Clear();
        monitor.Attachments.Clear();
        monitor.ClientKeys.Clear();
        monitor.ConversationKeys.Clear();
    }

    private static void PruneAttachments(NanoChatMonitorComponent monitor)
    {
        if (monitor.Attachments.Count == 0)
            return;

        var used = new HashSet<string>();
        foreach (var conversation in monitor.Conversations.Values)
        {
            foreach (var entry in conversation.Entries)
            {
                if (entry.AttachmentId is { } id)
                    used.Add(id);
            }
        }

        foreach (var id in monitor.Attachments.Keys.ToList())
        {
            if (!used.Contains(id))
                monitor.Attachments.Remove(id);
        }
    }


    private bool IsRelay(EntityUid uid)
    {
        return HasComp<SyndicateNanoChatRelayComponent>(uid);
    }

    /// <summary>
    ///     Pushes fresh state to every open viewer. Federation means a machine's own history is not the
    ///     only thing that changes what it shows.
    /// </summary>
    private void RefreshOpenViewers()
    {
        var query = EntityQueryEnumerator<NanoChatMonitorComponent>();
        while (query.MoveNext(out var uid, out var monitor))
        {
            if (_ui.IsUiOpen(uid, NanoChatMonitorUiKey.Key))
                UpdateUi((uid, monitor));
        }
    }

    private void OnMonitorRemoved(Entity<NanoChatMonitorComponent> ent, ref ComponentShutdown args)
    {
        InvalidateMergeCache();
        _revisions.Remove(ent.Owner);
    }

    private void OnMonitorMoved(Entity<NanoChatMonitorComponent> ent, ref EntParentChangedMessage args)
    {
        InvalidateMergeCache();
    }

    private void OnMonitorPowerChanged(Entity<NanoChatMonitorComponent> ent, ref PowerChangedEvent args)
    {
        InvalidateMergeCache();
        RefreshOpenViewers();
    }

    private void OnNetworkChanged(ref NanoChatNetworkChangedEvent args)
    {
        InvalidateMergeCache();
        RefreshOpenViewers();
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

        if (!TryGetPage(ent, args.ConversationKey, args.StartIndex, args.Latest, out var page, args.RequestId))
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
        if (!TryFindAttachment(ent, attachmentId, out var attachment) ||
            attachment.ImageData is not { Length: > 0 } imageData)
        {
            return false;
        }

        if (!TryStartPrint(ent))
            return false;

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

    public bool TryPrintLog(Entity<NanoChatMonitorComponent> ent, ulong clientKey, EntityUid actor)
    {
        // Printed from the same federated view the reader is looking at.
        if (!TryGetMerged(ent, clientKey, out var conversation) || conversation.Entries.Count == 0)
            return false;

        if (!TryStartPrint(ent))
            return false;

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
            $"#{DisplayNumber(conversation.NumberA, conversation.RedactedA)} and " +
            $"#{DisplayNumber(conversation.NumberB, conversation.RedactedB)} across {sheets.Count} sheet(s)");
        return true;
    }

    private List<string> BuildLogSheets(NanoChatMonitorComponent monitor, MergedConversation conversation)
    {
        var sheetSize = PaperContentSize();
        var maxLength = sheetSize * Math.Max(1, monitor.MaxLogSheets);

        var header = string.Join('\n',
            Loc.GetString("nanochat-monitor-print-log-title"),
            Loc.GetString("nanochat-monitor-print-log-participants",
                ("first", $"{conversation.NameA} (#{DisplayNumber(conversation.NumberA, conversation.RedactedA)})"),
                ("second", $"{conversation.NameB} (#{DisplayNumber(conversation.NumberB, conversation.RedactedB)})")),
            Loc.GetString("nanochat-monitor-print-log-count", ("count", conversation.Entries.Count)));

        var blocks = new List<string>(conversation.Entries.Count);
        foreach (var (entry, _) in conversation.Entries)
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
            // Paper is not a way around the redaction either.
            ("sender", entry.SenderName),
            ("number", DisplayNumber(entry.SenderNumber, entry.SenderRedacted)),
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
        ulong clientKey,
        int startIndex,
        bool latest,
        [NotNullWhen(true)] out NanoChatMonitorPageMessage? page,
        ulong requestId = 0)
    {
        page = null;

        if (!TryGetMerged(ent, clientKey, out var conversation))
            return false;

        var total = conversation.Entries.Count;
        var pageSize = NanoChatMonitorConstants.PageSize;

        var start = latest ? total - pageSize : startIndex;
        start = Math.Clamp(start, 0, Math.Max(0, total - 1));

        var count = Math.Max(0, Math.Min(pageSize, total - start));
        var entries = new List<NanoChatMonitorLogEntry>(count);

        for (var i = start; i < start + count; i++)
        {
            var (entry, source) = conversation.Entries[i];
            entries.Add(BuildLogEntry(source, conversation, entry));
        }

        page = new NanoChatMonitorPageMessage(clientKey, start, total, entries, conversation.Revision, requestId);
        return true;
    }

    public bool TryGetAttachment(
        Entity<NanoChatMonitorComponent> ent,
        string attachmentId,
        [NotNullWhen(true)] out NanoChatMonitorAttachmentMessage? message)
    {
        message = null;

        if (!TryFindAttachment(ent, attachmentId, out var attachment))
            return false;

        message = new NanoChatMonitorAttachmentMessage(
            attachmentId,
            attachment.FileName,
            attachment.ImageData,
            attachment.Caption,
            attachment.Description);

        return true;
    }

    /// <summary>
    ///     Finds an attachment on any machine the viewer is networked to. Ids are content hashes, so
    ///     whichever copy turns up first is the same image.
    /// </summary>
    private bool TryFindAttachment(
        Entity<NanoChatMonitorComponent> ent,
        string attachmentId,
        [NotNullWhen(true)] out NanoChatMonitorAttachment? attachment)
    {
        attachment = null;

        _network.GetFederatedServers(ent, _servers);

        foreach (var uid in _servers)
        {
            if (TryComp<NanoChatMonitorComponent>(uid, out var monitor) &&
                monitor.Attachments.TryGetValue(attachmentId, out attachment))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Resolves a viewer's handle back to a conversation in the current federated view. A handle for
    ///     history that has since gone off the network simply stops resolving.
    /// </summary>
    private bool TryGetMerged(
        Entity<NanoChatMonitorComponent> ent,
        ulong clientKey,
        [NotNullWhen(true)] out MergedConversation? conversation)
    {
        conversation = null;

        if (!ent.Comp.ConversationKeys.TryGetValue(clientKey, out var conversationKey))
            return false;

        return BuildMerged(ent).TryGetValue(conversationKey, out conversation);
    }

    private static NanoChatMonitorLogEntry BuildLogEntry(
        NanoChatMonitorComponent monitor,
        MergedConversation conversation,
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
            // The side, not the number, since two redacted ends look alike.
            entry.SenderNumber == conversation.NumberA,
            DisplayNumber(entry.SenderNumber, entry.SenderRedacted),
            entry.SenderName,
            entry.SenderJob,
            DisplayNumber(entry.RecipientNumber, entry.RecipientRedacted),
            entry.RecipientName,
            entry.RecipientJob,
            entry.Content,
            entry.Location,
            entry.AttachmentId,
            entry.AttachmentName,
            preview);
    }

    /// <summary>
    ///     The one value still withheld here rather than at record time: the number is kept as the
    ///     conversation key, so it is the only thing left to replace on the way out.
    /// </summary>
    private static string DisplayNumber(uint number, bool redacted)
    {
        return redacted ? NanoChatMonitorConstants.Redacted : $"{number:D4}";
    }

    private void UpdateUi(Entity<NanoChatMonitorComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, NanoChatMonitorUiKey.Key))
            return;

        _ui.SetUiState(ent.Owner, NanoChatMonitorUiKey.Key, new NanoChatMonitorUiState(BuildConversationSummaries(ent)));
    }

    public List<NanoChatMonitorConversationSummary> BuildConversationSummaries(Entity<NanoChatMonitorComponent> ent)
    {
        var merged = BuildMerged(ent);
        var conversations = new List<NanoChatMonitorConversationSummary>(merged.Count);

        foreach (var conversation in merged.Values)
        {
            if (conversation.Entries.Count == 0)
                continue;

            var last = conversation.Entries[^1].Entry;

            conversations.Add(new NanoChatMonitorConversationSummary(
                conversation.ClientKey,
                DisplayNumber(conversation.NumberA, conversation.RedactedA),
                conversation.NameA,
                conversation.JobA,
                DisplayNumber(conversation.NumberB, conversation.RedactedB),
                conversation.NameB,
                conversation.JobB,
                conversation.Entries.Count,
                last.Timestamp,
                conversation.Revision));
        }

        conversations.Sort(static (a, b) => b.LastTimestamp.CompareTo(a.LastTimestamp));
        return conversations;
    }

    public bool CanView(Entity<NanoChatMonitorComponent> ent, EntityUid actor)
    {
        // Deliberately no ID check; anyone who can reach the rack can read
        // it. Power and an actual log are still required.
        return IsPowered(ent) && KeepsLog(ent);
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
