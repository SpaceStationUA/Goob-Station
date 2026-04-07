using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Robust.Shared.Asynchronous;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Administration.Systems;

public sealed partial class BwoinkSystem
{
    [Dependency] private readonly ITaskManager _taskManager = default!;

    private readonly Dictionary<NetUserId, TicketRatingTracker> _ticketRatingTracker = new();
    private readonly Dictionary<string, Dictionary<NetUserId, byte>> _adminRatings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _adminDisplayNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<NetUserId, string> _pendingRatingTargets = new();
    private const int TicketRatingReminderMessageThreshold = 6;
    private bool _ratingsLoadStarted;

    public readonly record struct AdminScoreSummary(string AdminName, string AdminKey, float Average, int Count);

    private void PirateInitialize()
    {
        SubscribeNetworkEvent<BwoinkAHelpWindowClosed>(OnPirateAHelpWindowClosed);
        SubscribeNetworkEvent<BwoinkTicketRatingMessage>(OnPirateTicketRatingSubmitted);
        LoadPirateAdminRatings();
    }

    private void LoadPirateAdminRatings()
    {
        if (_ratingsLoadStarted)
            return;

        _ratingsLoadStarted = true;

        _ = Task.Run(async () =>
        {
            List<PirateAdminHelpRating> ratings;
            try
            {
                ratings = await _dbManager.GetPirateAdminHelpRatingsAsync();
            }
            catch (Exception e)
            {
                _sawmill.Error($"Failed to load AHelp ratings from DB: {e}");
                return;
            }

            _taskManager.RunOnMainThread(() => MergePirateAdminRatings(ratings));
        });
    }

    private void MergePirateAdminRatings(List<PirateAdminHelpRating> ratings)
    {
        foreach (var rating in ratings)
        {
            if (!_adminRatings.TryGetValue(rating.AdminKey, out var perPlayer))
            {
                perPlayer = new Dictionary<NetUserId, byte>();
                _adminRatings[rating.AdminKey] = perPlayer;
            }

            var playerId = new NetUserId(rating.PlayerUserId);
            if (!perPlayer.ContainsKey(playerId))
                perPlayer[playerId] = rating.Rating;

            if (!_adminDisplayNames.ContainsKey(rating.AdminKey)
                && !string.IsNullOrWhiteSpace(rating.AdminName))
            {
                _adminDisplayNames[rating.AdminKey] = rating.AdminName;
            }
        }
    }

    private void PirateOnPlayerStatusChanged(SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Disconnected)
            return;

        _ticketRatingTracker.Remove(e.Session.UserId);
        _pendingRatingTargets.Remove(e.Session.UserId);
    }

    private void PirateOnBwoinkInternal(BwoinkParams bwoinkParams)
    {
        if (bwoinkParams.Message.AdminOnly)
            return;

        if (!_ticketRatingTracker.TryGetValue(bwoinkParams.Message.UserId, out var tracker))
        {
            tracker = new TicketRatingTracker();
            _ticketRatingTracker[bwoinkParams.Message.UserId] = tracker;
        }

        if (bwoinkParams.SenderAdmin is null)
        {
            tracker.RegisterPlayerMessage();
        }
        else
        {
            var adminKey = GetAdminRatingKey(bwoinkParams);
            if (!string.IsNullOrWhiteSpace(adminKey))
            {
                tracker.RegisterAdminMessage(adminKey, bwoinkParams.SenderName);
                _adminDisplayNames[adminKey] = bwoinkParams.SenderName;
            }
        }
    }

    private void OnPirateAHelpWindowClosed(BwoinkAHelpWindowClosed msg, EntitySessionEventArgs args)
    {
        // Non-admins may only close/notify their own AHelp channel.
        if (msg.Channel != args.SenderSession.UserId)
            return;

        if (_adminManager.GetAdminData(args.SenderSession)?.HasFlag(AdminFlags.Adminhelp) == true)
            return;

        if (!_ticketRatingTracker.TryGetValue(msg.Channel, out var tracker))
            return;

        if (!tracker.CanPrompt(TicketRatingReminderMessageThreshold))
            return;

        var adminKey = tracker.LastAdminKey;
        var adminName = tracker.LastAdminName;
        if (string.IsNullOrWhiteSpace(adminKey) || string.IsNullOrWhiteSpace(adminName))
            return;

        _pendingRatingTargets[msg.Channel] = adminKey;
        _adminDisplayNames[adminKey] = adminName;

        var currentRating = GetCurrentRating(adminKey, msg.Channel);
        RaiseNetworkEvent(
            new BwoinkTicketRatingPrompt(currentRating, adminName),
            args.SenderSession.Channel);

        tracker.Reset();
    }

    private async void OnPirateTicketRatingSubmitted(BwoinkTicketRatingMessage msg, EntitySessionEventArgs args)
    {
        if (msg.Rating is < 1 or > 5)
            return;

        if (!_pendingRatingTargets.TryGetValue(args.SenderSession.UserId, out var adminKey))
            return;

        _pendingRatingTargets.Remove(args.SenderSession.UserId);

        if (!_adminRatings.TryGetValue(adminKey, out var ratingsForAdmin))
        {
            ratingsForAdmin = new Dictionary<NetUserId, byte>();
            _adminRatings[adminKey] = ratingsForAdmin;
        }

        ratingsForAdmin[args.SenderSession.UserId] = msg.Rating;

        var adminDisplay = _adminDisplayNames.TryGetValue(adminKey, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : adminKey;

        if (adminDisplay.Length > 128)
            adminDisplay = adminDisplay[..128];

        _adminDisplayNames[adminKey] = adminDisplay;

        _sawmill.Info(
            $"AHelp rating updated: player={args.SenderSession.Name} ({args.SenderSession.UserId}) admin={adminDisplay} rating={msg.Rating}/5");

        try
        {
            await _dbManager.UpsertPirateAdminHelpRatingAsync(
                adminKey,
                adminDisplay,
                args.SenderSession.UserId,
                msg.Rating);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to save AHelp rating to DB: {e}");
        }
    }

    private void OnPirateRoundRestartCleanup()
    {
        _ticketRatingTracker.Clear();
        _pendingRatingTargets.Clear();
    }

    private string? GetAdminRatingKey(BwoinkParams bwoinkParams)
    {
        if (bwoinkParams.SenderAdmin is null)
            return null;

        if (!bwoinkParams.FromWebhook && bwoinkParams.SenderId != SystemUserId)
            return $"id:{bwoinkParams.SenderId}";

        if (string.IsNullOrWhiteSpace(bwoinkParams.SenderName))
            return null;

        return $"name:{bwoinkParams.SenderName}";
    }

    private byte GetCurrentRating(string adminKey, NetUserId playerId)
    {
        if (_adminRatings.TryGetValue(adminKey, out var perPlayer)
            && perPlayer.TryGetValue(playerId, out var rating))
        {
            return rating;
        }

        return 0;
    }

    public IReadOnlyList<AdminScoreSummary> GetAdminScoreSummaries()
    {
        var results = new List<AdminScoreSummary>();

        foreach (var (adminKey, perPlayer) in _adminRatings)
        {
            if (perPlayer.Count == 0)
                continue;

            var total = 0;
            foreach (var rating in perPlayer.Values)
            {
                total += rating;
            }

            var average = total / (float) perPlayer.Count;
            var adminName = _adminDisplayNames.TryGetValue(adminKey, out var name)
                ? name
                : adminKey;

            results.Add(new AdminScoreSummary(adminName, adminKey, average, perPlayer.Count));
        }

        results.Sort((a, b) => b.Average.CompareTo(a.Average));
        return results;
    }

    private sealed class TicketRatingTracker
    {
        public int TotalMessages { get; private set; }
        public int PlayerMessages { get; private set; }
        public int AdminMessages { get; private set; }
        public string? LastAdminKey { get; private set; }
        public string? LastAdminName { get; private set; }

        public void RegisterPlayerMessage()
        {
            PlayerMessages++;
            TotalMessages++;
        }

        public void RegisterAdminMessage(string adminKey, string adminName)
        {
            AdminMessages++;
            TotalMessages++;
            LastAdminKey = adminKey;
            LastAdminName = adminName;
        }

        public bool CanPrompt(int threshold)
        {
            return TotalMessages >= threshold
                && PlayerMessages > 0
                && AdminMessages > 0
                && !string.IsNullOrWhiteSpace(LastAdminKey)
                && !string.IsNullOrWhiteSpace(LastAdminName);
        }

        public void Reset()
        {
            TotalMessages = 0;
            PlayerMessages = 0;
            AdminMessages = 0;
            LastAdminKey = null;
            LastAdminName = null;
        }
    }
}
