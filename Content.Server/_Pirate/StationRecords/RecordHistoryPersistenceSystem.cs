// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Shared._Pirate.StationRecords;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Access.Systems;
using Content.Shared.Preferences;
using Content.Shared.StationRecords;
using Robust.Shared.Configuration;

namespace Content.Server._Pirate.StationRecords;
// Only attach an owner after its history loads; an empty history must not erase saved rows.
public sealed record RecordHistoryOwner(int ProfileId);
public sealed class RecordHistoryPersistenceSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly RecordLogSystem _logs = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;

    private bool _enabled;
    private int _limit;
    private Task? _saveTask;
    private bool _savedThisRound;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.RecordsHistoryPersistence, value => _enabled = value, true);
        Subs.CVar(_cfg, CCVars.RecordsHistoryLimit, value => _limit = Math.Max(0, value), true);

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn, after: [typeof(StationRecordsSystem)]);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public override void Shutdown()
    {
        WaitForSave();
        base.Shutdown();
    }

    private async void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        if (!_enabled || !TryGetRecordKey(args.Mob, out var key))
            return;

        // A random character for the round isn't one of the player's slots, so it has no history.
        if (!TryFindSlot(_preferences.GetPreferences(args.Player.UserId), args.Profile, out var slot))
            return;

        var round = _ticker.RoundId;
        try
        {
            var profileId = await _db.GetCharacterProfileIdAsync(args.Player.UserId, slot);
            if (profileId == null)
                return;

            var history = await _db.GetRecordHistoryAsync(profileId.Value, _limit);
            if (round != _ticker.RoundId || !_records.TryGetRecord<GeneralStationRecord>(key, out _))
                return;

            _records.AddRecordEntry(key, new RecordHistoryOwner(profileId.Value));
            if (history.Count == 0)
                return;

            _logs.AddArchived(key, history);
            _records.Synchronize(key);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to restore the records history of {ToPrettyString(args.Mob)}: {e}");
        }
    }

    private void OnRoundEnd(RoundEndTextAppendEvent ev)
    {
        _savedThisRound = true;
        Save();
    }
    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        if (!_savedThisRound)
            Save();

        WaitForSave();
        _savedThisRound = false;
    }

    private void Save()
    {
        if (!_enabled)
            return;

        var snapshots = CollectSnapshots();
        if (snapshots.Count == 0)
            return;

        var limit = _limit;
        WaitForSave();
        // Off the game thread, so the restart can wait for it to finish.
        _saveTask = Task.Run(async () =>
        {
            try
            {
                await _db.SaveRecordHistoryAsync(snapshots, limit);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to save records histories: {e}");
            }
        });
    }
    private List<RecordHistorySnapshot> CollectSnapshots()
    {
        var books = new Dictionary<(int ProfileId, RecordLogBook Book), List<RecordLogEntry>>();

        var query = EntityQueryEnumerator<StationRecordsComponent>();
        while (query.MoveNext(out var station, out var stationRecords))
        {
            foreach (var (id, owner) in _records.GetRecordsOfType<RecordHistoryOwner>(station, stationRecords))
            {
                if (!_records.TryGetRecord<RecordLogs>(new StationRecordKey(id, station), out var logs, stationRecords))
                    logs = new RecordLogs();

                foreach (var book in Enum.GetValues<RecordLogBook>())
                {
                    if (!books.TryGetValue((owner.ProfileId, book), out var entries))
                        books[(owner.ProfileId, book)] = entries = new List<RecordLogEntry>();

                    foreach (var entry in logs.Book(book))
                    {
                        if (entry.Kind == RecordLogKind.Background || entries.Exists(e => SameEntry(e, entry)))
                            continue;

                        entries.Add(entry);
                    }
                }
            }
        }

        var snapshots = new List<RecordHistorySnapshot>(books.Count);
        foreach (var ((profileId, book), entries) in books)
        {
            snapshots.Add(new RecordHistorySnapshot(profileId, book, entries));
        }

        return snapshots;
    }

    private static bool SameEntry(RecordLogEntry a, RecordLogEntry b)
    {
        return a.Time == b.Time && a.Kind == b.Kind && a.Message == b.Message && a.AuthorName == b.AuthorName;
    }

    private void WaitForSave()
    {
        if (_saveTask == null)
            return;

        try
        {
            _saveTask.GetAwaiter().GetResult();
        }
        finally
        {
            _saveTask = null;
        }
    }

    private bool TryGetRecordKey(EntityUid mob, out StationRecordKey key)
    {
        key = default;
        // The key is on the ID card itself, which usually sits inside the PDA in the ID slot.
        if (!_idCard.TryFindIdCard(mob, out var idCard)
            || !TryComp<StationRecordKeyStorageComponent>(idCard, out var keyStorage)
            || keyStorage.Key is not { } storedKey)
        {
            return false;
        }

        key = storedKey;
        return true;
    }

    private static bool TryFindSlot(PlayerPreferences prefs, HumanoidCharacterProfile profile, out int slot)
    {
        if (prefs.TryIndexOfCharacter(profile, out slot))
            return true;

        foreach (var (index, saved) in prefs.Characters)
        {
            if (!saved.MemberwiseEquals(profile))
                continue;

            slot = index;
            return true;
        }

        return false;
    }
}
