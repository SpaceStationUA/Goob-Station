// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Threading.Tasks;
using Content.Server._Pirate.ServerCurrency;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    // A hold commits before any game events run. Mind transfers may themselves change a wallet.
    public async Task<PirateCurrencyResult> ReservePirateShop(PirateShopRequest request, string? rejection)
    {
        if (!request.Valid)
            return new("invalid_request", null);

        await using var db = await GetDb();
        var context = db.DbContext;
        var id = request.OperationId.ToString("D");
        var hash = request.Fingerprint();
        if (request.InspectOnly)
        {
            var prior = await context.PirateCurrencyReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.OperationId == id);
            return prior == null ? new("not_started", null)
                : prior.RequestHash == hash ? new(prior.State, prior.Balance) : new("operation_conflict", null);
        }

        await using var transaction = await context.Database.BeginTransactionAsync();
        var inserted = await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO pirate_currency_receipt (operation_id, request_hash, state) VALUES ({id}, {hash}, 'shop_held') ON CONFLICT (operation_id) DO NOTHING");
        var receipt = await context.PirateCurrencyReceipts.SingleAsync(r => r.OperationId == id);
        if (inserted == 0)
            return receipt.RequestHash == hash ? new(receipt.State, receipt.Balance) : new("operation_conflict", null);

        if (rejection != null)
            receipt.State = rejection;
        else
        {
            var players = await LockCurrencyPlayers(context, request.Source, request.Source);
            if (!players.TryGetValue(request.Source, out var player))
                receipt.State = "account_not_found";
            else
            {
                if (player.ServerCurrency < request.Amount && request.Amount > 0)
                    receipt.State = "insufficient_funds";
                else
                    player.ServerCurrency = checked(player.ServerCurrency - request.Amount);
                receipt.Balance = player.ServerCurrency;
            }
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(receipt.State, receipt.Balance, AppliedNow: receipt.State == "shop_held");
    }

    public async Task<PirateCurrencyResult> SettlePirateShop(PirateShopRequest request, string outcome)
    {
        if (!request.Valid || outcome is not ("delivered" or "offline" or "ineligible" or "stale_round" or "expired" or "unfulfilled"))
            return new("invalid_request", null);

        await using var db = await GetDb();
        var context = db.DbContext;
        await using var transaction = await context.Database.BeginTransactionAsync();
        var id = request.OperationId.ToString("D");
        var rows = context.Database.IsNpgsql()
            ? context.PirateCurrencyReceipts.FromSqlInterpolated($"SELECT * FROM pirate_currency_receipt WHERE operation_id = {id} FOR UPDATE")
            : context.PirateCurrencyReceipts.Where(r => r.OperationId == id);
        var receipt = await rows.SingleOrDefaultAsync();
        if (receipt == null || receipt.RequestHash != request.Fingerprint())
            return new("operation_conflict", null);
        if (receipt.State != "shop_held")
            return new(receipt.State, receipt.Balance);

        var players = await LockCurrencyPlayers(context, request.Source, request.Source);
        var player = players[request.Source];
        if (outcome != "delivered")
            player.ServerCurrency = checked(player.ServerCurrency + request.Amount);
        receipt.State = outcome;
        receipt.Balance = player.ServerCurrency;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(outcome, receipt.Balance, AppliedNow: true);
    }
}
