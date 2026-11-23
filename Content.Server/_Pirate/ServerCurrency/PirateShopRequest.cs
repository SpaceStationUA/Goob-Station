// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server._Pirate.ServerCurrency;

public sealed record PirateShopRequest(Guid OperationId, Guid Source, string Item, int Amount, int RoundId,
    long ExpiresAt, bool InspectOnly = false)
{
    public bool Valid => OperationId != Guid.Empty && Source != Guid.Empty && RoundId > 0 && ExpiresAt > 0
        && Item switch
        {
            "mercenary" => Amount is >= 0 and <= 250,
            "magespawn" => Amount is > 0 and <= 250,
            "sparkles" or "rgblight" => Amount is > 0 and <= 10,
            "catears" => Amount is > 0 and <= 5,
            _ => false,
        };

    public string Fingerprint() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        FormattableString.Invariant($"discord_shop|{Source:D}|{RoundId}|{Item}|{Amount}|{ExpiresAt}"))));
}

/// <summary>Delivered only on the game thread through the authenticated server API.</summary>
public sealed class PirateShopEvent(PirateShopRequest request) : EntityEventArgs
{
    public PirateShopRequest Request { get; } = request;
    public Task<PirateCurrencyResult> Result = Task.FromResult(new PirateCurrencyResult("unavailable", null));
}
