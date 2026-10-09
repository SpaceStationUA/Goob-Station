// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Shared._Pirate.CustomMarkings;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Verbs;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.CustomMarkings;

/// <summary>
/// Keeps each player's library of custom markings and the art behind them, and hands art to clients by hash.
/// Bodies get their markings from the profile (SharedHumanoidAppearanceSystem); this system never touches them
/// except when an admin removes or blocks what one wears.
/// </summary>
public sealed partial class CustomMarkingSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const int MaxCachedArt = 2048;
    private static readonly TimeSpan SaveCooldown = TimeSpan.FromSeconds(1);

    /// <summary>Art requests a session may have outstanding; one comes back every <see cref="ArtRequestRefill"/>.</summary>
    private const int ArtRequestBudget = 1024;
    private static readonly TimeSpan ArtRequestRefill = TimeSpan.FromMilliseconds(50);

    /// <summary>The same for reading the library and deleting from it, which each cost a database trip.</summary>
    private const int LibraryBudget = 16;
    private static readonly TimeSpan LibraryRefill = TimeSpan.FromMilliseconds(500);

    /// <summary>Art by hash, including in-flight reads; missing results are removed after the read.</summary>
    private readonly Dictionary<string, Task<CustomMarkingStoredArt?>> _art = new();

    private readonly Dictionary<ICommonSession, TimeSpan> _nextSave = new();

    /// <summary>When each session's art request budget will be full again.</summary>
    private readonly Dictionary<ICommonSession, TimeSpan> _artBudgetFull = new();

    private readonly Dictionary<ICommonSession, TimeSpan> _libraryBudgetFull = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CustomMarkingLibraryRequestEvent>(OnLibraryRequest);
        SubscribeNetworkEvent<CustomMarkingSaveEvent>(OnSave);
        SubscribeNetworkEvent<CustomMarkingDeleteEvent>(OnDelete);
        SubscribeNetworkEvent<CustomMarkingArtRequestEvent>(OnArtRequest);
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        _player.PlayerStatusChanged += OnPlayerStatusChanged;

        PurgeUnusedArt();
    }

    /// <summary>
    /// Clears out art nothing uses any more, once as the server starts: with no round on yet, no body can be
    /// wearing art that neither a library nor a saved character still points at.
    /// </summary>
    private async void PurgeUnusedArt()
    {
        var days = _cfg.GetCVar(CustomMarkingCVars.UnusedArtDays);
        if (days <= 0)
            return;

        try
        {
            var (found, deleted) = await _db.PurgeUnusedCustomMarkingArtAsync(TimeSpan.FromDays(days));
            if (found > 0 || deleted > 0)
                Log.Info($"Custom marking art: deleted {deleted} drawings unused for {days} days, found {found} more that nothing uses.");
        }
        catch (Exception e)
        {
            Log.Error($"Cleaning up unused custom marking art threw: {e}");
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    /// <summary>
    /// The id art is stored under: a hash of its pixels, so the same drawing is only ever stored once. Frame times
    /// and an erase mask go into it only when the art has them, which leaves a plain still marking's hash that of
    /// its pixels alone. The three parts differ in length, so no two kinds of art hash the same bytes.
    /// </summary>
    public static string Hash(CustomMarkingArt art)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(art.Pixels);
        if (CustomMarkingRules.PackFrameTimes(art.GetFrameTimes()) is { } times)
            hash.AppendData(times);

        if (art.HasErase())
            hash.AppendData(art.Erase);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private bool Enabled => _cfg.GetCVar(CustomMarkingCVars.Enabled);

    private async void OnLibraryRequest(CustomMarkingLibraryRequestEvent msg, EntitySessionEventArgs args)
    {
        if (Spend(_libraryBudgetFull, args.SenderSession, LibraryRefill, LibraryBudget))
            await SendLibrary(args.SenderSession);
    }

    private async Task SendLibrary(ICommonSession session)
    {
        List<PirateCustomMarking> rows;
        try
        {
            rows = await _db.GetCustomMarkingsAsync(session.UserId);
        }
        catch (Exception e)
        {
            Log.Error($"Reading the custom marking library of {session.Name} threw: {e}");
            return;
        }

        if (session.Status != SessionStatus.Disconnected)
            RaiseNetworkEvent(new CustomMarkingLibraryEvent(rows.Select(ToEntry).ToList()), session);
    }

    private static CustomMarkingEntry ToEntry(PirateCustomMarking row)
    {
        var placement = (CustomMarkingPlacement) row.Placement;
        return new CustomMarkingEntry(row.Id, row.Name, row.ArtHash, Enum.IsDefined(placement) ? placement : CustomMarkingPlacement.Skin);
    }

    private async void OnSave(CustomMarkingSaveEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var result = await Save(session, msg);

        if (session.Status == SessionStatus.Disconnected)
            return;

        CustomMarkingEntry? entry = result.Entry is { } row ? ToEntry(row) : null;
        RaiseNetworkEvent(new CustomMarkingSaveResultEvent(msg.Request, entry, result.PreviousHash, result.Error), session);
        if (entry != null)
            await SendLibrary(session);
    }

    private async Task<CustomMarkingSaveResult> Save(ICommonSession session, CustomMarkingSaveEvent msg)
    {
        if (!Enabled)
            return CustomMarkingSaveResult.Fail("wf-custom-marking-error-disabled");

        var now = _timing.RealTime;
        if (_nextSave.TryGetValue(session, out var next) && now < next)
            return CustomMarkingSaveResult.Fail("wf-custom-marking-error-cooldown");

        if (!Enum.IsDefined(msg.Placement))
            return CustomMarkingSaveResult.Fail("wf-custom-marking-error-invalid");

        string? hash = null;
        CustomMarkingStoredArt? stored = null;
        if (msg.Pixels != null)
        {
            // With erasing turned off, a mask is dropped rather than refused: the drawing is still good.
            var erase = _cfg.GetCVar(CustomMarkingCVars.EraseBody) ? msg.Erase : null;
            var maxFrames = Math.Max(1, _cfg.GetCVar(CustomMarkingCVars.MaxFrames));
            if (CustomMarkingArt.FromPixels(msg.Pixels, msg.FrameTimes, erase, maxFrames) is not { } art)
                return CustomMarkingSaveResult.Fail("wf-custom-marking-error-invalid");

            if (art.IsBlank() && !art.HasErase())
                return CustomMarkingSaveResult.Fail("wf-custom-marking-error-blank");

            hash = Hash(art);
            stored = new CustomMarkingStoredArt(
                art.ToPng(),
                CustomMarkingRules.PackFrameTimes(art.GetFrameTimes()),
                art.HasErase() ? art.Erase : null);
        }

        var name = CustomMarkingRules.CleanName(msg.Name);
        if (name.Length == 0)
            name = Loc.GetString("wf-custom-marking-default-name");

        // Only saves that reach the database are spaced out; the checks above cost nothing.
        _nextSave[session] = now + SaveCooldown;

        CustomMarkingSaveResult result;
        try
        {
            result = await _db.SaveCustomMarkingAsync(session.UserId, msg.Id, name, (int) msg.Placement, hash, stored,
                _cfg.GetCVar(CustomMarkingCVars.LibraryLimit), _cfg.GetCVar(CustomMarkingCVars.DailyArtLimit));
        }
        catch (Exception e)
        {
            Log.Error($"Saving a custom marking for {session.Name} threw: {e}");
            return CustomMarkingSaveResult.Fail("wf-custom-marking-error-failed");
        }

        // Awaits resume on the game thread, so the cache is only touched there.
        if (result.Entry != null && hash != null)
        {
            // Moderation may have committed while this save awaited its game-thread continuation.
            // The next request must read the canonical row and its current moderation status.
            _art.Remove(hash);
            _adminLog.Add(LogType.Identity, LogImpact.Low,
                $"{session:player} saved custom marking \"{name}\" with art {hash}");
        }

        return result;
    }

    private async void OnDelete(CustomMarkingDeleteEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!Spend(_libraryBudgetFull, session, LibraryRefill, LibraryBudget))
            return;

        try
        {
            await _db.DeleteCustomMarkingAsync(session.UserId, msg.Id);
        }
        catch (Exception e)
        {
            Log.Error($"Deleting a custom marking of {session.Name} threw: {e}");
        }

        await SendLibrary(session);
    }

    private async void OnArtRequest(CustomMarkingArtRequestEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (msg.Hashes is not { Count: > 0 and <= CustomMarkingRules.MaxRequestedArt } hashes)
            return;

        foreach (var hash in hashes)
        {
            if (!CustomMarkingRules.IsValidHash(hash) || !Spend(_artBudgetFull, session, ArtRequestRefill, ArtRequestBudget))
                continue;

            var art = Enabled ? await GetArt(hash) : null;
            if (session.Status == SessionStatus.Disconnected)
                return;

            RaiseNetworkEvent(
                new CustomMarkingArtEvent(hash, art?.Png, CustomMarkingRules.UnpackFrameTimes(art?.FrameTimes), art?.Erase),
                session);
        }
    }

    /// <summary>
    /// Takes one request out of a session's budget, which holds <paramref name="budget"/> requests and gets one
    /// back every <paramref name="refill"/>. False when it is spent. <paramref name="full"/> holds when each
    /// session's budget will be full again.
    /// </summary>
    private bool Spend(Dictionary<ICommonSession, TimeSpan> full, ICommonSession session, TimeSpan refill, int budget)
    {
        var now = _timing.RealTime;
        var at = full.GetValueOrDefault(session);
        if (at < now)
            at = now;

        at += refill;
        if (at - now > refill * budget)
            return false;

        full[session] = at;
        return true;
    }

    private async Task<CustomMarkingStoredArt?> GetArt(string hash)
    {
        if (!_art.TryGetValue(hash, out var known))
        {
            if (_art.Count >= MaxCachedArt)
                _art.Clear();

            known = ReadArt(hash);
            _art[hash] = known;
        }

        // Pirate: misses must not evict real art, including when the DB read completes synchronously.
        var art = await known;
        if (art == null && _art.TryGetValue(hash, out var cached) && ReferenceEquals(known, cached))
            _art.Remove(hash);
        return art;
    }

    private async Task<CustomMarkingStoredArt?> ReadArt(string hash)
    {
        try
        {
            return await _db.GetCustomMarkingArtAsync(hash);
        }
        catch (Exception e)
        {
            Log.Error($"Reading custom marking art {hash} threw: {e}");
            return null;
        }
    }

    /// <summary>
    /// Blocks art, or lifts a block. Blocked art comes off every body, is no longer sent to anyone and can't be
    /// saved again. Returns false when the server has no art with this hash.
    /// </summary>
    public async Task<bool> SetBlocked(string hash, bool blocked, string actor)
    {
        if (!CustomMarkingRules.IsValidHash(hash))
            return false;

        Guid? found;
        try
        {
            found = await _db.SetCustomMarkingArtBlockedAsync(hash, blocked);
        }
        catch (Exception e)
        {
            Log.Error($"Blocking custom marking art {hash} threw: {e}");
            return false;
        }

        if (found is not { } uploader)
            return false;

        _art.Remove(hash);
        _adminLog.Add(LogType.Identity, LogImpact.Medium,
            $"{actor} {(blocked ? "blocked" : "unblocked")} custom marking art {hash}, first saved by {uploader}");

        if (!blocked)
            return true;

        var query = EntityQueryEnumerator<HumanoidAppearanceComponent>();
        while (query.MoveNext(out var uid, out var humanoid))
        {
            if (humanoid.CustomMarkings.RemoveAll(marking => marking.Hash == hash) > 0)
                Dirty(uid, humanoid);
        }

        // Clients drop art they already hold when told there is none.
        RaiseNetworkEvent(new CustomMarkingArtEvent(hash, null));
        return true;
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(args.Target, out var humanoid)
            || humanoid.CustomMarkings.Count == 0
            || !TryComp<ActorComponent>(args.User, out var actor)
            || !_admin.HasAdminFlag(actor.PlayerSession, AdminFlags.Admin))
            return;

        var user = args.User;
        var target = args.Target;
        var admin = actor.PlayerSession.Name;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("wf-custom-marking-admin-remove"),
            Category = VerbCategory.Admin,
            Impact = LogImpact.Medium,
            ConfirmationPopup = true,
            Act = () =>
            {
                if (!TryComp<HumanoidAppearanceComponent>(target, out var body))
                    return;

                _adminLog.Add(LogType.Identity, LogImpact.Medium,
                    $"{ToPrettyString(user):actor} removed the custom markings {Worn(body)} from {ToPrettyString(target):target}");
                body.CustomMarkings.Clear();
                Dirty(target, body);
            },
        });

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("wf-custom-marking-admin-block"),
            Category = VerbCategory.Admin,
            Impact = LogImpact.High,
            ConfirmationPopup = true,
            Act = async () =>
            {
                if (!TryComp<HumanoidAppearanceComponent>(target, out var body))
                    return;

                foreach (var hash in body.CustomMarkings.Select(marking => marking.Hash).Distinct().ToArray())
                {
                    await SetBlocked(hash, true, admin);
                }
            },
        });
    }

    private static string Worn(HumanoidAppearanceComponent humanoid)
    {
        return string.Join(", ", humanoid.CustomMarkings.Select(marking => marking.Hash));
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _art.Clear();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        _nextSave.Remove(args.Session);
        _artBudgetFull.Remove(args.Session);
        _libraryBudgetFull.Remove(args.Session);
    }
}
