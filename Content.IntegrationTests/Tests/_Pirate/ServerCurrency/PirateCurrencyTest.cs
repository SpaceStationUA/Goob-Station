// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Linq;
using Content.Server._Pirate.ServerCurrency;
using Content.Server.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests._Pirate.ServerCurrency;

[TestFixture]
public sealed class PirateCurrencyTest
{
    [Test]
    public async Task ReplaysAndCompetingDebitsPreserveTheBalance()
    {
        await using var pair = await PoolManager.GetServerClient();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SqliteServerDbContext>().UseSqlite(connection).Options;
        var db = new ServerDbSqlite(() => options, true, pair.Server.ResolveDependency<IConfigurationManager>(),
            true, pair.Server.ResolveDependency<ILogManager>().GetSawmill("db.ops"));
        var sender = new NetUserId(Guid.NewGuid());
        var receiver = new NetUserId(Guid.NewGuid());
        await db.UpdatePlayerRecord(sender, "currency_sender", IPAddress.Loopback, null);
        await db.UpdatePlayerRecord(receiver, "currency_receiver", IPAddress.Loopback, null);
        await db.SetServerCurrency(sender, 100);

        var gift = new PirateCurrencyRequest(Guid.NewGuid(), sender, receiver, 70, "transfer");
        var first = await db.ApplyPirateCurrency(gift, null);
        Assert.That(first, Is.EqualTo(new PirateCurrencyResult("applied", 30, 70, true)));
        var replay = await db.ApplyPirateCurrency(gift, null);
        Assert.That(replay.State, Is.EqualTo("applied"));
        Assert.That(replay.AppliedNow, Is.False);
        Assert.That(replay.Balance, Is.EqualTo(30));
        Assert.That((await db.ApplyPirateCurrency(gift with { Amount = 1 }, null)).State, Is.EqualTo("operation_conflict"));
        Assert.That(await db.GetServerCurrency(receiver), Is.EqualTo(70));

        var purchase = new PirateCurrencyRequest(Guid.NewGuid(), sender, null, 25, "purchase");
        var other = purchase with { OperationId = Guid.NewGuid() };
        var results = await Task.WhenAll(db.ApplyPirateCurrency(purchase, null), db.ApplyPirateCurrency(other, null));
        Assert.That(results.Select(r => r.State), Is.EquivalentTo(new[] { "applied", "insufficient_funds" }));
        Assert.That(await db.GetServerCurrency(sender), Is.EqualTo(5));
        Assert.That(await db.ModifyServerCurrency(sender, 100), Is.EqualTo(105));
        var rejected = results[0].State == "applied" ? other : purchase;
        Assert.That((await db.ApplyPirateCurrency(rejected, null)).State, Is.EqualTo("insufficient_funds"));
        Assert.That(await db.GetServerCurrency(sender), Is.EqualTo(105));

        await db.SetServerCurrency(receiver, int.MaxValue);
        Assert.That((await db.ApplyPirateCurrency(gift with { OperationId = Guid.NewGuid() }, null)).State, Is.EqualTo("balance_overflow"));
        var missing = gift with { OperationId = Guid.NewGuid(), Target = Guid.NewGuid() };
        Assert.That((await db.ApplyPirateCurrency(missing, null)).State, Is.EqualTo("account_not_found"));
        Assert.That(await db.GetServerCurrency(sender), Is.EqualTo(105));
        Assert.That(await db.GetServerCurrency(receiver), Is.EqualTo(int.MaxValue));

        // The token's admin note and its debit are committed together. Retrying cannot issue another note.
        var note = new AdminNote
        {
            PlayerUserId = sender, CreatedById = sender, LastEditedById = sender,
            Message = "test currency token", CreatedAt = DateTime.UtcNow, LastEditedAt = DateTime.UtcNow,
        };
        var token = purchase with { OperationId = Guid.NewGuid(), Amount = 100 };
        var bought = await db.ApplyPirateCurrency(token, note);
        Assert.That(bought.NoteId, Is.GreaterThan(0));
        Assert.That((await db.ApplyPirateCurrency(token, note)).AppliedNow, Is.False);
        Assert.That((await db.ApplyPirateCurrency(token with { OperationId = Guid.NewGuid() }, note)).NoteId, Is.Null);
        await using var inspect = new SqliteServerDbContext(options);
        Assert.That(await inspect.AdminNotes.CountAsync(n => n.Message == note.Message), Is.EqualTo(1));
        Assert.That(await db.GetServerCurrency(sender), Is.EqualTo(5));
        Assert.That(await db.AdjustPirateCurrency(sender, 50, PirateCurrencyAdjustment.RemoveUpTo), Is.EqualTo((5, 0)));

        await pair.CleanReturnAsync();
    }
}
