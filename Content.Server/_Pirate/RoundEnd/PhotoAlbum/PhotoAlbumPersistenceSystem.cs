// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server._Pirate.Photo;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Shared._Pirate.Photo;
using Content.Shared.GameTicking;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Pirate.RoundEnd.PhotoAlbum;

public sealed class PhotoAlbumPersistenceSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly PhotoSystem _photo = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    private readonly Dictionary<EntityUid, ResolvedAlbumPersistenceState> _resolvedAlbumStates = new();
    private Task? _persistTask;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PersistentPhotoAlbumComponent, ComponentStartup>(OnPersistentPhotoAlbumStartup);
        SubscribeLocalEvent<PersistentPhotoAlbumComponent, ComponentShutdown>(OnPersistentPhotoAlbumShutdown);
        SubscribeLocalEvent<PersistentPhotoAlbumComponent, SelectedLoadoutEntitySpawnedEvent>(OnSelectedLoadoutAlbumSpawned);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public override void Shutdown()
    {
        WaitForPendingPersistence();
        base.Shutdown();
    }

    private void OnPersistentPhotoAlbumStartup(
        EntityUid uid,
        PersistentPhotoAlbumComponent component,
        ref ComponentStartup args)
    {
        if (string.IsNullOrWhiteSpace(component.OwnerId))
            return;

        RestoreStaticAlbumSnapshot(uid, component);
    }

    private void OnPersistentPhotoAlbumShutdown(
        EntityUid uid,
        PersistentPhotoAlbumComponent component,
        ref ComponentShutdown args)
    {
        _resolvedAlbumStates.Remove(uid);
    }

    private void OnSelectedLoadoutAlbumSpawned(
        EntityUid uid,
        PersistentPhotoAlbumComponent component,
        ref SelectedLoadoutEntitySpawnedEvent args)
    {
        EnsureComp<SelectedLoadoutPersistentPhotoAlbumComponent>(uid);
    }

    private async void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        var prefs = _preferences.GetPreferences(ev.Player.UserId);
        var selectedSlot = prefs.SelectedCharacterIndex;

        var albums = new List<(EntityUid Uid, PhotoAlbumComponent Album, PersistentPhotoAlbumComponent Persistence)>();
        var query = EntityQueryEnumerator<PhotoAlbumComponent, PersistentPhotoAlbumComponent, SelectedLoadoutPersistentPhotoAlbumComponent>();
        while (query.MoveNext(out var uid, out var album, out var persistence, out _))
        {
            if (!IsOwnedBy(uid, ev.Mob) || _resolvedAlbumStates.ContainsKey(uid))
                continue;

            albums.Add((uid, album, persistence));
        }

        foreach (var (uid, album, persistence) in albums)
        {
            try
            {
                var state = await ResolvePersistenceStateAsync(ev.Player.UserId, selectedSlot, persistence);
                if (state == null || Deleted(uid))
                    continue;

                var snapshot = await _db.GetPersistentPhotoAlbumSnapshotAsync(
                    state.Value.OwnerKind,
                    state.Value.ProfileId,
                    state.Value.OwnerId,
                    state.Value.AlbumKey);
                if (Deleted(uid) || !IsOwnedBy(uid, ev.Mob))
                    continue;

                _resolvedAlbumStates[uid] = state.Value;

                if (snapshot == null)
                    continue;

                persistence.IsPublic = persistence.SupportsPrivacy
                    ? snapshot.IsPublic
                    : true;
                RestoreAlbumSnapshot(uid, album, snapshot);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to restore persistent photo album {ToPrettyString(uid)} for {ev.Player}: {ex}");
            }
        }
    }

    private void OnRoundEndTextAppend(RoundEndTextAppendEvent ev)
    {
        var snapshots = CollectAlbumSnapshots();
        if (snapshots.Count == 0)
            return;

        _persistTask = PersistSnapshotsAsync(snapshots);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        WaitForPendingPersistence();
    }

    private async Task PersistSnapshotsAsync(List<PersistentPhotoAlbumSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            try
            {
                await _db.UpsertPersistentPhotoAlbumSnapshotAsync(
                    snapshot.OwnerKind,
                    snapshot.ProfileId,
                    snapshot.OwnerId,
                    snapshot.AlbumKey,
                    snapshot.IsPublic,
                    snapshot.Photos);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to persist photo album {FormatSnapshotIdentity(snapshot)}: {ex}");
            }
        }
    }

    private void WaitForPendingPersistence()
    {
        if (_persistTask == null)
            return;

        try
        {
            _persistTask.GetAwaiter().GetResult();
        }
        finally
        {
            _persistTask = null;
        }
    }

    private List<PersistentPhotoAlbumSnapshot> CollectAlbumSnapshots()
    {
        var snapshots = new Dictionary<(string OwnerKind, int? ProfileId, string? OwnerId, string AlbumKey), PersistentPhotoAlbumSnapshot>();
        var query = EntityQueryEnumerator<PhotoAlbumComponent, PersistentPhotoAlbumComponent>();
        while (query.MoveNext(out var uid, out var album, out var persistence))
        {
            if (!TryResolvePersistenceState(uid, persistence, out var state))
                continue;

            if (!_container.TryGetContainer(uid, album.ContainerId, out var container))
                continue;

            var photos = new List<PersistentPhotoData>(container.ContainedEntities.Count);
            foreach (var item in container.ContainedEntities)
            {
                if (!TryComp<PhotoCardComponent>(item, out var photoCard) ||
                    !_photo.TryCreatePersistentPhotoData(photoCard, out var data))
                {
                    continue;
                }

                photos.Add(data);
            }

            var key = (state.OwnerKind, state.ProfileId, state.OwnerId, state.AlbumKey);
            snapshots[key] = new PersistentPhotoAlbumSnapshot
            {
                OwnerKind = state.OwnerKind,
                ProfileId = state.ProfileId,
                OwnerId = state.OwnerId,
                AlbumKey = state.AlbumKey,
                IsPublic = persistence.EffectiveIsPublic,
                SavedAt = DateTime.UtcNow,
                Photos = photos
            };
        }

        return new List<PersistentPhotoAlbumSnapshot>(snapshots.Values);
    }

    private void RestoreAlbumSnapshot(
        EntityUid uid,
        PhotoAlbumComponent component,
        PersistentPhotoAlbumSnapshot snapshot)
    {
        if (!_container.TryGetContainer(uid, component.ContainerId, out var container))
            return;

        var coords = Transform(uid).Coordinates;
        foreach (var photoData in snapshot.Photos)
        {
            var photoUid = Spawn("PhotoCard", coords);
            if (!TryComp<PhotoCardComponent>(photoUid, out var photoCard) ||
                !_photo.TryApplyPersistentPhotoData(photoUid, photoCard, photoData) ||
                !_container.Insert(photoUid, container))
            {
                Del(photoUid);
                continue;
            }

            photoCard.IsArchivedAlbumPhoto = true;
            _photo.UpdatePhotoCardExamineDescription(photoUid, photoCard);
        }
    }

    private async Task<ResolvedAlbumPersistenceState?> ResolvePersistenceStateAsync(
        NetUserId userId,
        int selectedSlot,
        PersistentPhotoAlbumComponent component)
    {
        if (!string.IsNullOrWhiteSpace(component.OwnerId))
        {
            return new ResolvedAlbumPersistenceState(
                component.OwnerKind,
                null,
                component.OwnerId,
                component.AlbumKey);
        }

        if (!string.Equals(component.OwnerKind, PersistentPhotoAlbumOwnerKinds.Profile, StringComparison.Ordinal))
            return null;

        var profileId = await _db.GetCharacterProfileIdAsync(userId, selectedSlot);
        return profileId == null
            ? null
            : new ResolvedAlbumPersistenceState(
                component.OwnerKind,
                profileId.Value,
                null,
                component.AlbumKey);
    }

    private async void RestoreStaticAlbumSnapshot(EntityUid uid, PersistentPhotoAlbumComponent persistence)
    {
        if (!TryComp<PhotoAlbumComponent>(uid, out var album))
            return;

        var ownerId = persistence.OwnerId;
        if (string.IsNullOrWhiteSpace(ownerId))
            return;

        try
        {
            var snapshot = await _db.GetPersistentPhotoAlbumSnapshotAsync(
                persistence.OwnerKind,
                null,
                ownerId,
                persistence.AlbumKey);
            if (snapshot == null || Deleted(uid) || !TryComp<PhotoAlbumComponent>(uid, out album))
                return;

            persistence.IsPublic = persistence.SupportsPrivacy
                ? snapshot.IsPublic
                : true;
            RestoreAlbumSnapshot(uid, album, snapshot);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to restore persistent photo album {ToPrettyString(uid)}: {ex}");
        }
    }

    private bool TryResolvePersistenceState(
        EntityUid uid,
        PersistentPhotoAlbumComponent persistence,
        out ResolvedAlbumPersistenceState state)
    {
        if (!string.IsNullOrWhiteSpace(persistence.OwnerId))
        {
            state = new ResolvedAlbumPersistenceState(
                persistence.OwnerKind,
                null,
                persistence.OwnerId,
                persistence.AlbumKey);
            return true;
        }

        if (_resolvedAlbumStates.TryGetValue(uid, out var resolved))
        {
            state = resolved;
            return true;
        }

        state = default;
        return false;
    }

    private static string FormatSnapshotIdentity(PersistentPhotoAlbumSnapshot snapshot)
    {
        return $"{snapshot.OwnerKind}/{snapshot.ProfileId?.ToString() ?? snapshot.OwnerId ?? "<none>"}/{snapshot.AlbumKey}";
    }

    private bool IsOwnedBy(EntityUid uid, EntityUid owner)
    {
        var current = uid;
        var depth = 0;

        while (depth < 64 && _container.TryGetContainingContainer(current, out var container))
        {
            if (container.Owner == owner)
                return true;

            current = container.Owner;
            depth++;
        }

        return false;
    }

    private readonly record struct ResolvedAlbumPersistenceState(
        string OwnerKind,
        int? ProfileId,
        string? OwnerId,
        string AlbumKey);
}
