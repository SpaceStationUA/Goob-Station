// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Tasks;
using Content.Server._Pirate.ServerCurrency;

namespace Content.Server.Database;

public partial interface IServerDbManager
{
    Task<PirateCurrencyResult> ReservePirateShop(PirateShopRequest request, string? rejection);
    Task<PirateCurrencyResult> SettlePirateShop(PirateShopRequest request, string outcome);
    Task<PirateCurrencyResult> ApplyPirateCurrency(PirateCurrencyRequest request, AdminNote? purchasedNote);
    Task<(int Old, int New)> AdjustPirateCurrency(Guid user, int amount, PirateCurrencyAdjustment adjustment);
}

public sealed partial class ServerDbManager
{
    public Task<PirateCurrencyResult> ReservePirateShop(PirateShopRequest request, string? rejection)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.ReservePirateShop(request, rejection));
    }

    public Task<PirateCurrencyResult> SettlePirateShop(PirateShopRequest request, string outcome)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.SettlePirateShop(request, outcome));
    }

    public Task<PirateCurrencyResult> ApplyPirateCurrency(PirateCurrencyRequest request, AdminNote? purchasedNote)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.ApplyPirateCurrency(request, purchasedNote));
    }

    public Task<(int Old, int New)> AdjustPirateCurrency(Guid user, int amount, PirateCurrencyAdjustment adjustment)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.AdjustPirateCurrency(user, amount, adjustment));
    }
}
