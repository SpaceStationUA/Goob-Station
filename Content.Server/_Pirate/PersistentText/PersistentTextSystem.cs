// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Paper;
using Content.Server.Preferences.Managers;
using Content.Shared._Pirate.PersistentText;
using Content.Shared._Pirate.Photo;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Pirate.PersistentText;

public sealed class PersistentTextSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    private readonly Dictionary<EntityUid, ResolvedTextPersistenceState> _resolvedTextStates = new();
    private Task? _persistTask;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PersistentTextComponent, ComponentStartup>(OnPersistentTextStartup);
        SubscribeLocalEvent<PersistentTextComponent, ComponentShutdown>(OnPersistentTextShutdown);
        SubscribeLocalEvent<PersistentTextComponent, SelectedLoadoutEntitySpawnedEvent>(OnSelectedLoadoutTextSpawned);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public override void Shutdown()
    {
        WaitForPendingPersistence();
        base.Shutdown();
    }

    private void OnPersistentTextStartup(
        EntityUid uid,
        PersistentTextComponent component,
        ref ComponentStartup args)
    {
        if (string.IsNullOrWhiteSpace(component.OwnerId))
            return;

        RestoreStaticTextSnapshot(uid, component);
    }

    private void OnPersistentTextShutdown(
        EntityUid uid,
        PersistentTextComponent component,
        ref ComponentShutdown args)
    {
        _resolvedTextStates.Remove(uid);
    }

    private void OnSelectedLoadoutTextSpawned(
        EntityUid uid,
        PersistentTextComponent component,
        ref SelectedLoadoutEntitySpawnedEvent args)
    {
        EnsureComp<SelectedLoadoutPersistentTextComponent>(uid);
    }

    private async void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        var prefs = _preferences.GetPreferences(ev.Player.UserId);
        var selectedSlot = prefs.SelectedCharacterIndex;

        var texts = new List<(EntityUid Uid, PaperComponent Paper, PersistentTextComponent Persistence)>();
        var query = EntityQueryEnumerator<PaperComponent, PersistentTextComponent, SelectedLoadoutPersistentTextComponent>();
        while (query.MoveNext(out var uid, out var paper, out var persistence, out _))
        {
            if (!IsOwnedBy(uid, ev.Mob) || _resolvedTextStates.ContainsKey(uid))
                continue;

            texts.Add((uid, paper, persistence));
        }

        foreach (var (uid, paper, persistence) in texts)
        {
            try
            {
                var state = await ResolvePersistenceStateAsync(ev.Player.UserId, selectedSlot, persistence);
                if (state == null || Deleted(uid))
                    continue;

                var snapshot = await _db.GetPersistentTextSnapshotAsync(
                    state.Value.OwnerKind,
                    state.Value.ProfileId,
                    state.Value.OwnerId,
                    state.Value.StorageKey);
                if (Deleted(uid) || !IsOwnedBy(uid, ev.Mob))
                    continue;

                _resolvedTextStates[uid] = state.Value;

                if (snapshot == null || string.IsNullOrEmpty(snapshot.Content))
                    continue;

                _paper.SetContent((uid, paper), snapshot.Content);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to restore persistent text {ToPrettyString(uid)} for {ev.Player}: {ex}");
            }
        }
    }

    private void OnRoundEndTextAppend(RoundEndTextAppendEvent ev)
    {
        var snapshots = CollectTextSnapshots();
        if (snapshots.Count == 0)
            return;

        _persistTask = PersistSnapshotsAsync(snapshots);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        WaitForPendingPersistence();
    }

    private async Task PersistSnapshotsAsync(List<PersistentTextSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            try
            {
                await _db.UpsertPersistentTextSnapshotAsync(
                    snapshot.OwnerKind,
                    snapshot.ProfileId,
                    snapshot.OwnerId,
                    snapshot.StorageKey,
                    snapshot.Content);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to persist text {FormatSnapshotIdentity(snapshot)}: {ex}");
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

    private List<PersistentTextSnapshot> CollectTextSnapshots()
    {
        var snapshots = new Dictionary<(string OwnerKind, int? ProfileId, string? OwnerId, string StorageKey), PersistentTextSnapshot>();
        var query = EntityQueryEnumerator<PaperComponent, PersistentTextComponent>();
        while (query.MoveNext(out var uid, out var paper, out var persistence))
        {
            if (!TryResolvePersistenceState(uid, persistence, out var state))
                continue;

            var key = (state.OwnerKind, state.ProfileId, state.OwnerId, state.StorageKey);
            snapshots[key] = new PersistentTextSnapshot
            {
                OwnerKind = state.OwnerKind,
                ProfileId = state.ProfileId,
                OwnerId = state.OwnerId,
                StorageKey = state.StorageKey,
                SavedAt = DateTime.UtcNow,
                Content = paper.Content
            };
        }

        return new List<PersistentTextSnapshot>(snapshots.Values);
    }

    private async void RestoreStaticTextSnapshot(EntityUid uid, PersistentTextComponent persistence)
    {
        if (!TryComp<PaperComponent>(uid, out var paper))
            return;

        var ownerId = persistence.OwnerId;
        if (string.IsNullOrWhiteSpace(ownerId))
            return;

        try
        {
            var snapshot = await _db.GetPersistentTextSnapshotAsync(
                persistence.OwnerKind,
                null,
                ownerId,
                persistence.StorageKey);
            if (snapshot == null || Deleted(uid) || !TryComp<PaperComponent>(uid, out paper))
                return;

            if (string.IsNullOrEmpty(snapshot.Content))
                return;

            _paper.SetContent((uid, paper), snapshot.Content);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to restore persistent text {ToPrettyString(uid)}: {ex}");
        }
    }

    private async Task<ResolvedTextPersistenceState?> ResolvePersistenceStateAsync(
        NetUserId userId,
        int selectedSlot,
        PersistentTextComponent component)
    {
        if (!string.IsNullOrWhiteSpace(component.OwnerId))
        {
            return new ResolvedTextPersistenceState(
                component.OwnerKind,
                null,
                component.OwnerId,
                component.StorageKey);
        }

        if (!string.Equals(component.OwnerKind, PersistentTextOwnerKinds.Profile, StringComparison.Ordinal))
            return null;

        var profileId = await _db.GetCharacterProfileIdAsync(userId, selectedSlot);
        return profileId == null
            ? null
            : new ResolvedTextPersistenceState(
                component.OwnerKind,
                profileId.Value,
                null,
                component.StorageKey);
    }

    private bool TryResolvePersistenceState(
        EntityUid uid,
        PersistentTextComponent persistence,
        out ResolvedTextPersistenceState state)
    {
        if (!string.IsNullOrWhiteSpace(persistence.OwnerId))
        {
            state = new ResolvedTextPersistenceState(
                persistence.OwnerKind,
                null,
                persistence.OwnerId,
                persistence.StorageKey);
            return true;
        }

        if (_resolvedTextStates.TryGetValue(uid, out var resolved))
        {
            state = resolved;
            return true;
        }

        state = default;
        return false;
    }

    private static string FormatSnapshotIdentity(PersistentTextSnapshot snapshot)
    {
        return $"{snapshot.OwnerKind}/{snapshot.ProfileId?.ToString() ?? snapshot.OwnerId ?? "<none>"}/{snapshot.StorageKey}";
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

    private readonly record struct ResolvedTextPersistenceState(
        string OwnerKind,
        int? ProfileId,
        string? OwnerId,
        string StorageKey);
}
