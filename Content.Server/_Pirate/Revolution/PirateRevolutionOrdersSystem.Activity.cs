// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.GameTicking;
using Content.Shared._Pirate.Revolution;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Revolution;

public sealed partial class PirateRevolutionOrdersSystem
{
    internal readonly record struct OrderOrigin(RevOrderSource Source, string? Admin = null)
    {
        public static OrderOrigin Automatic => new(RevOrderSource.Automatic);

        public static OrderOrigin Manual(ICommonSession? admin) =>
            admin == null ? new(RevOrderSource.ServerConsole) : new(RevOrderSource.Admin, admin.Name);
    }

    private const int HistoryPageSize = 10;
    private readonly List<RevOrderLogEntry> _history = new();
    private bool _automaticOrders = true;

    internal event Action? OrderActivityChanged;

    private void ResetActivity()
    {
        _automaticOrders = true;
        _history.Clear();
        OrderActivityChanged?.Invoke();
    }

    internal bool SetAutomaticOrders(int round, bool enabled)
    {
        if (round != OrderRound || _automaticOrders == enabled)
            return false;

        _automaticOrders = enabled;
        if (enabled)
        {
            foreach (var rule in GetActiveOrderRules())
                Schedule(rule);
        }

        OrderActivityChanged?.Invoke();
        return true;
    }

    internal RevOrderActivityState GetOrderActivity(int page)
    {
        var lastPage = Math.Max(0, (_history.Count - 1) / HistoryPageSize);
        page = Math.Clamp(page, 0, lastPage);
        var entries = new List<RevOrderLogEntry>(HistoryPageSize);
        for (var index = _history.Count - 1 - page * HistoryPageSize;
            index >= 0 && entries.Count < HistoryPageSize;
            index--)
        {
            entries.Add(_history[index]);
        }

        return new RevOrderActivityState(OrderRound, _automaticOrders, GetActiveOrderRules().Count > 0,
            page, lastPage, entries);
    }

    private void RecordOrder(OrderOrigin origin, string order, string message)
    {
        var time = GameTicker.RunLevel == GameRunLevel.PreRoundLobby ? TimeSpan.Zero : GameTicker.RoundDuration();
        _history.Add(new RevOrderLogEntry(time, origin.Source, origin.Admin, order, message));
        OrderActivityChanged?.Invoke();
    }
}
