// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Server._Pirate.Photo;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Server.StationRecords.Systems;
using System.Text;
using Content.Shared._Pirate;
using Content.Shared._Pirate.StationRecords;
using Content.Shared.GameTicking;
using Content.Shared.Access.Systems;
using Robust.Shared.Network;
using Content.Shared.CriminalRecords;
using Content.Shared.Hands.Components;
using Content.Shared.Preferences;
using Content.Shared.StationRecords;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.StationRecords;

public sealed class RecordPortraitSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly PhotoSystem _photo = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;

    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
    private static readonly SoundSpecifier UploadSound = new SoundPathSpecifier("/Audio/_Pirate/Machines/terminal_prompt_confirm.ogg");
    private static readonly TimeSpan PrintDelay = TimeSpan.FromSeconds(2.3f);
    private const int MaxNumber = 999999;

    private readonly HashSet<EntityUid> _activePrintJobs = new();

    private readonly Dictionary<(EntityUid Station, uint Id), NetUserId> _pendingRenders = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AfterGeneralRecordCreatedEvent>(OnGeneralRecordCreated);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn, after: [typeof(StationRecordsSystem)]);
        SubscribeNetworkEvent<RecordPortraitRenderResultEvent>(OnRenderResult);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _pendingRenders.Clear());
    }

    private void OnGeneralRecordCreated(AfterGeneralRecordCreatedEvent ev)
    {
        if (!string.IsNullOrWhiteSpace(ev.Record.Species))
            EnsurePortrait(ev.Key).ProfileSnapshot = new(ev.Profile);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        // The key is on the ID card itself, which usually sits inside the PDA in the ID slot.
        if (!_idCard.TryFindIdCard(args.Mob, out var idCard)
            || !TryComp<StationRecordKeyStorageComponent>(idCard, out var keyStorage)
            || keyStorage.Key is not { } key
            || !_records.TryGetRecord<GeneralStationRecord>(key, out _))
        {
            return;
        }

        var record = EnsurePortrait(key);
        EnsureNumber(key, record);
        FreezePlacard(key, record);

        if (record.ImageData is { Length: > 0 } || record.ProfileSnapshot == null)
            return;

        _records.TryGetRecord<GeneralStationRecord>(key, out var general);
        _pendingRenders[(key.OriginStation, key.Id)] = args.Player.UserId;
        // The server cannot render the character, so request its owner's client to do it.
        RaiseNetworkEvent(new RecordPortraitRenderRequestEvent
        {
            Station = GetNetEntity(key.OriginStation),
            RecordId = key.Id,
            Profile = record.ProfileSnapshot,
            JobPrototype = general?.JobPrototype,
        }, args.Player);
    }

    private void OnRenderResult(RecordPortraitRenderResultEvent ev, EntitySessionEventArgs args)
    {
        var station = GetEntity(ev.Station);

        // Only the requested player's session may store this render.
        if (!_pendingRenders.TryGetValue((station, ev.RecordId), out var user) || user != args.SenderSession.UserId)
            return;

        _pendingRenders.Remove((station, ev.RecordId));
        StoreGenerated(new StationRecordKey(ev.RecordId, station), ev.ImageData);
    }

    public RecordPortrait EnsurePortrait(StationRecordKey key)
    {
        if (_records.TryGetRecord<RecordPortrait>(key, out var portrait))
            return portrait;

        portrait = new RecordPortrait();
        _records.AddRecordEntry(key, portrait);
        return portrait;
    }

    public HumanoidCharacterProfile? GetSnapshot(StationRecordKey key)
    {
        return _records.TryGetRecord<RecordPortrait>(key, out var portrait) ? portrait.ProfileSnapshot : null;
    }

    public void EnsureSnapshot(StationRecordKey key, HumanoidCharacterProfile profile)
    {
        var portrait = EnsurePortrait(key);
        portrait.ProfileSnapshot ??= profile;
    }

    public void UpdateSnapshot(StationRecordKey key, HumanoidCharacterProfile profile)
    {
        if (_records.TryGetRecord<RecordPortrait>(key, out var portrait)
            && (portrait.ProfileSnapshot != null || portrait.ImageData is { Length: > 0 }))
        {
            portrait.ProfileSnapshot = profile;
        }
    }

    public void RenameSnapshot(StationRecordKey key, string name)
    {
        if (_records.TryGetRecord<RecordPortrait>(key, out var portrait) && portrait.ProfileSnapshot != null)
            portrait.ProfileSnapshot = portrait.ProfileSnapshot.WithName(name);
    }

    private GeneralStationRecord? GetGeneral(StationRecordKey key)
    {
        if (_records.TryGetRecord<GeneralStationRecord>(key, out var general))
            return general;

        return _records.TryGetRecord<CriminalRecord>(key, out var criminal) ? criminal.GeneralRecordSnapshot : null;
    }

    public void FreezePlacard(StationRecordKey key, RecordPortrait record)
    {
        if (record.Name != null || GetGeneral(key) is not { } general)
            return;

        record.Name = general.Name;
        record.BirthYear = general.Age > 0 ? PirateStationCalendar.CurrentYear - general.Age : 0;
    }

    public RecordPortraitData? GetPortrait(StationRecordKey key)
    {
        if (!_records.TryGetRecord<RecordPortrait>(key, out var record))
            return null;

        EnsureNumber(key, record);
        return new RecordPortraitData
        {
            ImageData = record.ImageData,
            IsRender = record.IsRender,
            Snapshot = record.ProfileSnapshot,
            Number = record.Number,
            Name = record.Name,
            BirthYear = record.BirthYear,
        };
    }

    public void EnsureNumber(StationRecordKey key, RecordPortrait record)
    {
        if (record.Number != 0)
            return;

        var general = GetGeneral(key);

        var taken = new HashSet<int>();
        foreach (var (_, other) in _records.GetRecordsOfType<RecordPortrait>(key.OriginStation))
        {
            if (other.Number != 0)
                taken.Add(other.Number);
        }

        // Hash identity for a stable placard number, then probe the rare collision.
        var number = (int) (Fnv1a($"{general?.DNA}|{general?.Fingerprint}") % MaxNumber) + 1;
        for (var i = 0; i < MaxNumber && taken.Contains(number); i++)
        {
            number = number % MaxNumber + 1;
        }

        record.Number = number;
    }

    public void Print(EntityUid console, EntityUid user, StationRecordKey key, byte[]? renderedImage)
    {
        if (_activePrintJobs.Contains(console))
        {
            _popup.PopupEntity(Loc.GetString("records-console-photo-print-busy"), console, user);
            return;
        }

        if (!_records.TryGetRecord<RecordPortrait>(key, out var record))
            return;

        byte[]? imageData = null;
        byte[]? previewData = null;

        if (record.ImageData is { Length: > 0 } existingImage && !record.IsRender)
        {
            if (_photo.TryPreparePhotoCardData(existingImage, record.PreviewData, out var preparedImage, out var preparedPreview))
            {
                imageData = preparedImage;
                previewData = preparedPreview;

                if (!ByteArraysEqual(record.ImageData, preparedImage) ||
                    !ByteArraysEqual(record.PreviewData, preparedPreview))
                {
                    record.ImageData = [.. preparedImage];
                    record.PreviewData = preparedPreview == null ? null : [.. preparedPreview];
                    _records.Synchronize(key);
                }
            }
        }
        else if (renderedImage is { Length: > 0 }
                 && _photo.TryPreparePhotoCardData(renderedImage, null, out var mugshot, out var mugshotPreview))
        {
            imageData = mugshot;
            previewData = mugshotPreview;
        }
        else if (record.ImageData is { Length: > 0 } render
                 && _photo.TryPreparePhotoCardData(render, record.PreviewData, out var bareRender, out var bareRenderPreview))
        {
            imageData = bareRender;
            previewData = bareRenderPreview;
        }

        if (imageData is not { Length: > 0 })
            return;

        var printedName = GetGeneral(key)?.Name;

        _activePrintJobs.Add(console);
        _audio.PlayPvs(PrintSound, console);
        _popup.PopupEntity(Loc.GetString("records-console-photo-print-start"), console, user);

        Timer.Spawn(PrintDelay, () =>
        {
            _activePrintJobs.Remove(console);

            if (TerminatingOrDeleted(console))
                return;

            var printed = Spawn("PhotoCard", Transform(console).Coordinates);
            if (!TryComp<PhotoCardComponent>(printed, out var photo))
            {
                Del(printed);
                return;
            }

            _photo.TrySetPhotoCardData(printed, photo, imageData, previewData, customName: printedName);

            if (!TerminatingOrDeleted(user))
                _popup.PopupEntity(Loc.GetString("records-console-photo-print-complete"), printed, user);
        });
    }

    public bool Upload(EntityUid console, EntityUid user, StationRecordKey key)
    {
        if (!TryGetHeldPhotoCard(user, out var heldPhoto)
            || heldPhoto.ImageData is not { Length: > 0 } imageData
            || GetGeneral(key) == null
            || !_photo.TryPreparePhotoCardData(imageData, heldPhoto.PreviewData, out var preparedImage, out var preparedPreview))
        {
            return false;
        }

        var record = EnsurePortrait(key);
        record.ImageData = [.. preparedImage];
        record.PreviewData = preparedPreview == null ? null : [.. preparedPreview];
        record.IsRender = false;

        _audio.PlayPvs(UploadSound, console);
        _records.Synchronize(key);
        return true;
    }

    public bool StoreGenerated(StationRecordKey key, byte[] imageData)
    {
        if (!_records.TryGetRecord<RecordPortrait>(key, out var record)
            || record.ProfileSnapshot == null
            || record.ImageData is { Length: > 0 }
            || !_photo.TryPreparePhotoCardData(imageData, null, out var preparedImage, out var preparedPreview))
        {
            return false;
        }

        record.ImageData = [.. preparedImage];
        record.PreviewData = preparedPreview == null ? null : [.. preparedPreview];
        record.IsRender = true;
        EnsureNumber(key, record);
        FreezePlacard(key, record);
        _records.Synchronize(key);
        return true;
    }

    public void CancelPrintJob(EntityUid console)
    {
        _activePrintJobs.Remove(console);
    }

    private bool TryGetHeldPhotoCard(EntityUid user, [NotNullWhen(true)] out PhotoCardComponent? photo)
    {
        photo = null;

        if (!TryComp<HandsComponent>(user, out var hands))
            return false;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (TryComp(held, out photo))
                return true;
        }

        return false;
    }

    private static uint Fnv1a(string text)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash = unchecked((hash ^ b) * 16777619u);
        }

        return hash;
    }

    private static bool ByteArraysEqual(byte[]? left, byte[]? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left == null || right == null || left.Length != right.Length)
            return false;

        return left.AsSpan().SequenceEqual(right);
    }
}
