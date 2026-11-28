// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server._Pirate.Paint; // Pirate: diary drawings
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Paper;
using Content.Server.Preferences.Managers;
using Content.Shared._Pirate.Paper;
using Content.Server._Pirate.PersistentText;
using Content.Shared._Pirate.PersistentText;
using Content.Shared._Pirate.Photo;
using Content.Shared.Bed.Cryostorage; // Pirate: save diaries on cryo
using Content.Shared.GameTicking;
using Content.Shared.Mind;
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
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    [Dependency] private readonly DiaryDrawingSystem _diaryDrawings = default!; // Pirate: diary drawings
    private readonly Dictionary<EntityUid, ResolvedTextPersistenceState> _resolvedTextStates = new();
    private Task? _persistTask;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PersistentTextComponent, ComponentStartup>(OnPersistentTextStartup);
        SubscribeLocalEvent<PersistentTextComponent, ComponentShutdown>(OnPersistentTextShutdown);
        SubscribeLocalEvent<PersistentTextComponent, SelectedLoadoutEntitySpawnedEvent>(OnSelectedLoadoutTextSpawned);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        // Pirate: save diaries on cryo - a global handler, because CryostorageSystem already
        // owns the directed CryostorageComponent/EntInsertedIntoContainerMessage subscription.
        SubscribeLocalEvent<EntInsertedIntoContainerMessage>(OnCryostorageInserted);
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
                // Pirate: remember the content read before awaiting so a write that happens
                // while the snapshot is being fetched is not clobbered by the restore below.
                var contentBefore = paper.Content;

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

                // Pirate: persistent text (diaries) - bind to the loadout owner at round start.
                // A fresh spawn always belongs to the spawning player; the stored text
                // (if any) is restored below regardless.
                if (persistence.SupportCharacterName)
                {
                    // Pirate: bind by CHARACTER - use the spawned mob's actual in-world name,
                    // not the mind (minds are recreated on exit/re-enter) and not the raw
                    // profile name (renames before spawn complete would break the write check,
                    // which compares against the actor's current name).
                    var characterName = Name(ev.Mob);

                    if (snapshot?.OwnerUserId != null &&
                        snapshot.OwnerUserId.Value != ev.Player.UserId.UserId)
                    {
                        Log.Warning($"Persistent text {ToPrettyString(uid)} snapshot belongs to another user; adopting the spawning player.");
                    }

                    persistence.OwnerUserId = ev.Player.UserId;
                    persistence.OwnerCharacterName = characterName;

                    _paper.UpdatePersistentTextName(uid, persistence);
                }

                if (snapshot == null)
                    continue;

                var restoredContent = PrepareRestoredContent(uid, snapshot.Content);
                if (string.IsNullOrEmpty(restoredContent))
                    continue;

                // Pirate: the player may have written while the snapshot was loading;
                // never overwrite their edit with the stale snapshot.
                if (!string.Equals(paper.Content, contentBefore, StringComparison.Ordinal))
                {
                    Log.Debug($"Skipped persistent text restore for {ToPrettyString(uid)}: content changed while loading.");
                    continue;
                }

                _paper.SetContent((uid, paper), restoredContent);

                // Pirate: diary drawings - the restored leaves carry their own chalk layers.
                _diaryDrawings.Refresh(uid);
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

        // Pirate: persistent text (diaries) - run the save on the thread pool so its
        // continuations never post back to the main thread's synchronization context.
        // Starting it on the main thread would make any later wait for it
        // (round restart cleanup / shutdown) deadlock the game thread.
        QueuePersistence(snapshots);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        WaitForPendingPersistence();

        // Pirate: persistent text (diaries) - direct restarts (commands/votes) can skip
        // RoundEndTextAppend, so save synchronously here before entities are flushed.
        try
        {
            var snapshots = CollectTextSnapshots();
            if (snapshots.Count > 0)
            {
                // Pirate: persistent text (diaries) - the save must run on the thread pool.
                // Persisting directly here would capture the main thread's synchronization
                // context, and the synchronous wait below would then block the very thread
                // the save's continuation needs - a permanent freeze between "Restarting
                // round!" and "Wiping all minds". Waiting on the thread-pool task is safe:
                // it completes independently of the main thread.
                QueuePersistence(snapshots);
                WaitForPendingPersistence();
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed final persistent text save on round restart: {ex}");
        }
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
                    snapshot.Content,
                    snapshot.OwnerCharacterName,
                    snapshot.OwnerUserId);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to persist text {FormatSnapshotIdentity(snapshot)}: {ex}");
            }
        }
    }

    /// <summary>
    /// Pirate: diary drawings - saves queued mid-round (cryo) must not be dropped by a
    /// later round-end save, so every queued task is awaited by <see cref="WaitForPendingPersistence"/>.
    /// </summary>
    private void QueuePersistence(List<PersistentTextSnapshot> snapshots)
    {
        var task = Task.Run(() => PersistSnapshotsAsync(snapshots));
        _persistTask = _persistTask == null ? task : Task.WhenAll(_persistTask, task);
    }

    /// <summary>
    /// Pirate: save diaries (text and drawings) the moment their owner is put into cryo,
    /// so leaving the round through cryo never loses the last page or doodle.
    /// </summary>
    private void OnCryostorageInserted(EntInsertedIntoContainerMessage args)
    {
        if (!TryComp<CryostorageComponent>(args.Container.Owner, out var cryo)
            || args.Container.ID != cryo.ContainerId)
            return;

        var snapshots = CollectTextSnapshots(args.Entity);
        if (snapshots.Count == 0)
            return;

        QueuePersistence(snapshots);
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

    private List<PersistentTextSnapshot> CollectTextSnapshots(EntityUid? owner = null)
    {
        var snapshots = new Dictionary<(string OwnerKind, int? ProfileId, string? OwnerId, string StorageKey), PersistentTextSnapshot>();
        var query = EntityQueryEnumerator<PaperComponent, PersistentTextComponent>();
        while (query.MoveNext(out var uid, out var paper, out var persistence))
        {
            if (owner != null && !IsOwnedBy(uid, owner.Value))
                continue;

            if (!TryResolvePersistenceState(uid, persistence, out var state))
                continue;

            // Pirate: diary drawings - the chalk layer lives on the leaf on screen, so it is
            // tucked into that leaf first; drawings then travel inside the text content.
            _diaryDrawings.Flush(uid);

            var key = (state.OwnerKind, state.ProfileId, state.OwnerId, state.StorageKey);
            snapshots[key] = new PersistentTextSnapshot
            {
                OwnerKind = state.OwnerKind,
                ProfileId = state.ProfileId,
                OwnerId = state.OwnerId,
                StorageKey = state.StorageKey,
                OwnerCharacterName = persistence.OwnerCharacterName,
                OwnerUserId = persistence.OwnerUserId?.UserId,
                SavedAt = DateTime.UtcNow,
                Content = paper.Content
            };
        }

        return new List<PersistentTextSnapshot>(snapshots.Values);
    }

    private string PrepareRestoredContent(EntityUid uid, string content)
    {
        content = PaperPageFormat.StripLegacyCoverMeta(content);
        if (HasComp<PaperPagesComponent>(uid))
            content = PaperPageFormat.StripStamps(content);

        return content;
    }

    private async void RestoreStaticTextSnapshot(EntityUid uid, PersistentTextComponent persistence)
    {
        if (!TryComp<PaperComponent>(uid, out var paper))
            return;

        var ownerId = persistence.OwnerId;
        if (string.IsNullOrWhiteSpace(ownerId))
            return;

        // What the entity looked like before the round-trip: a write landing while the
        // snapshot loads must win over the database, both text and binding.
        var preContent = paper.Content;
        var preCharacterName = persistence.OwnerCharacterName;
        var preUserId = persistence.OwnerUserId;

        try
        {
            var snapshot = await _db.GetPersistentTextSnapshotAsync(
                persistence.OwnerKind,
                null,
                ownerId,
                persistence.StorageKey);
            if (snapshot == null || Deleted(uid) || !TryComp<PaperComponent>(uid, out paper))
                return;

            // The content changed while waiting (someone wrote): keep their text and stop.
            if (!string.Equals(paper.Content, preContent, StringComparison.Ordinal))
                return;

            // Binding changed while waiting (BindPersistentTextOwner already ran): keep it.
            var bindingChanged = !string.Equals(persistence.OwnerCharacterName, preCharacterName, StringComparison.Ordinal)
                || persistence.OwnerUserId != preUserId;
            if (!bindingChanged)
            {
                // Pirate: persistent text (diaries) - restore bound owner
                persistence.OwnerCharacterName = snapshot.OwnerCharacterName;
                persistence.OwnerUserId = snapshot.OwnerUserId != null
                    ? new NetUserId(snapshot.OwnerUserId.Value)
                    : null;
                if (persistence.SupportCharacterName)
                    _paper.UpdatePersistentTextName(uid, persistence);
            }

            if (string.IsNullOrEmpty(snapshot.Content))
                return;

            var restoredContent = PrepareRestoredContent(uid, snapshot.Content);
            if (string.IsNullOrEmpty(restoredContent))
                return;

            _paper.SetContent((uid, paper), restoredContent);

            // Pirate: diary drawings - load the chalk layer of the leaf on screen.
            _diaryDrawings.Refresh(uid);
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
