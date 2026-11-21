// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Tasks;
using Content.Goobstation.Common.ServerCurrency;
using Content.Server._Pirate.ServerCurrency;
using Content.Server.Database;
using Robust.Shared.Network;

namespace Content.Goobstation.Server.ServerCurrency;

public sealed partial class ServerCurrencyManager : IPirateCurrencyOperations
{
    // Called on the game thread. Publish only committed changes, once, to both online clients.
    public PirateCurrencyResult Apply(PirateCurrencyRequest request, AdminNote? purchasedNote = null)
    {
        var task = Task.Run(() => _db.ApplyPirateCurrency(request, purchasedNote));
        _ = TrackPendingAsync(task);
        var result = task.GetAwaiter().GetResult();
        if (!result.AppliedNow)
            return result;

        var source = result.Balance!.Value;
        var destination = request.Target is {} recipient ? $" to {recipient}" : "";
        _sawmill.Info($"Currency {request.Kind} {request.OperationId}: {request.Amount} from {request.Source}{destination}. Remaining: {source}");
        NotifyPirateBalance(new NetUserId(request.Source), source + request.Amount, source);
        if (request.Target is {} target && result.TargetBalance is {} balance)
            NotifyPirateBalance(new NetUserId(target), balance - request.Amount, balance);
        return result;
    }

    public int RemoveUpTo(NetUserId player, int amount)
    {
        var (before, after) = Adjust(player, amount, PirateCurrencyAdjustment.RemoveUpTo);
        return before - after;
    }

    private (int Old, int New) Adjust(NetUserId player, int amount, PirateCurrencyAdjustment adjustment)
    {
        var task = Task.Run(() => _db.AdjustPirateCurrency(player, amount, adjustment));
        _ = TrackPendingAsync(task);
        var balances = task.GetAwaiter().GetResult();
        NotifyPirateBalance(player, balances.Old, balances.New);
        return balances;
    }

    private void NotifyPirateBalance(NetUserId id, int before, int after)
    {
        if (before != after && _player.TryGetSessionById(id, out var session))
            BalanceChange?.Invoke(new PlayerBalanceChangeEvent(session, id, after, before));
    }
}
