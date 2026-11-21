// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server._Pirate.ServerCurrency;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task<PirateCurrencyResult> ApplyPirateCurrency(PirateCurrencyRequest request, AdminNote? purchasedNote)
    {
        if (!request.Valid || purchasedNote != null && (request.Kind != "purchase" || purchasedNote.PlayerUserId != request.Source))
            return new("invalid_request", null);

        await using var db = await GetDb();
        var context = db.DbContext;
        await using var transaction = await context.Database.BeginTransactionAsync();
        var id = request.OperationId.ToString("D");
        var hash = request.Fingerprint(purchasedNote?.Message);
        // ON CONFLICT waits for a concurrent first claimant to commit before we read its outcome.
        var inserted = await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO pirate_currency_receipt (operation_id, request_hash, state) VALUES ({id}, {hash}, 'pending') ON CONFLICT (operation_id) DO NOTHING");
        var receipt = await context.PirateCurrencyReceipts.SingleAsync(r => r.OperationId == id);
        if (inserted == 0)
            return receipt.RequestHash == hash ? new(receipt.State, receipt.Balance) : new("operation_conflict", null);

        var accounts = await LockCurrencyPlayers(context, request.Source, request.Target ?? request.Source);
        accounts.TryGetValue(request.Source, out var source);
        Player? target = null;
        if (request.Target is {} targetId)
            accounts.TryGetValue(targetId, out target);

        if (source == null || request.Target != null && target == null)
            receipt.State = "account_not_found";
        else if (source.ServerCurrency < request.Amount)
            receipt.State = "insufficient_funds";
        else if (target != null && target.ServerCurrency > int.MaxValue - request.Amount)
            receipt.State = "balance_overflow";
        else
        {
            source.ServerCurrency -= request.Amount;
            if (target != null)
                target.ServerCurrency += request.Amount;
            if (purchasedNote != null)
            {
                // SQLite admin notes use an explicitly assigned ID, see ServerDbSqlite.AddAdminNote.
                if (context.Database.IsSqlite())
                    purchasedNote.Id = (await context.AdminNotes.MaxAsync(n => (int?) n.Id) ?? 0) + 1;
                context.AdminNotes.Add(purchasedNote);
            }
            receipt.State = "applied";
        }

        receipt.Balance = source?.ServerCurrency;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(receipt.State, receipt.Balance, target?.ServerCurrency, receipt.State == "applied", receipt.State == "applied" ? purchasedNote?.Id : null);
    }

    public async Task<(int Old, int New)> AdjustPirateCurrency(Guid user, int amount, PirateCurrencyAdjustment adjustment)
    {
        await using var db = await GetDb();
        var context = db.DbContext;
        await using var transaction = await context.Database.BeginTransactionAsync();
        var accounts = await LockCurrencyPlayers(context, user, user);
        if (!accounts.TryGetValue(user, out var player))
            return (0, 0);

        var before = player.ServerCurrency;
        player.ServerCurrency = adjustment switch
        {
            PirateCurrencyAdjustment.Add => checked(before + amount),
            PirateCurrencyAdjustment.Set => amount,
            PirateCurrencyAdjustment.RemoveUpTo => before - Math.Clamp(amount, 0, Math.Max(before, 0)),
            _ => throw new ArgumentOutOfRangeException(nameof(adjustment)),
        };
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return (before, player.ServerCurrency);
    }

    private static Task<Dictionary<Guid, Player>> LockCurrencyPlayers(ServerDbContext context, Guid first, Guid second)
    {
        // SQLite starts an immediate write transaction. PostgreSQL locks the same two rows in a
        // consistent order for opposite-direction transfers and concurrent gameplay rewards.
        var query = context.Database.IsNpgsql()
            ? context.Player.FromSqlInterpolated(
                $"SELECT * FROM player WHERE user_id IN ({first}, {second}) ORDER BY user_id FOR UPDATE")
            : context.Player.Where(p => p.UserId == first || p.UserId == second);
        return query.ToDictionaryAsync(p => p.UserId);
    }
}
