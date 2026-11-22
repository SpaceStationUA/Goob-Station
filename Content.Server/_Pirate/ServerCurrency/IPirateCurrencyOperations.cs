// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using Content.Server.Database;
using Robust.Shared.Network;

namespace Content.Server._Pirate.ServerCurrency;

public enum PirateCurrencyAdjustment
{
    Add,
    Set,
    RemoveUpTo,
}

public readonly record struct PirateCurrencyRequest(Guid OperationId, Guid Source, Guid? Target, int Amount, string Kind)
{
    public const string ThanksReward = "thanks_reward";
    public int SourceDelta => Kind == ThanksReward ? Amount : -Amount;

    public bool Valid => OperationId != Guid.Empty && Source != Guid.Empty && Amount > 0
        && Kind switch
        {
            "purchase" => Target == null,
            "transfer" => Target.HasValue && Target != Guid.Empty && Target != Source,
            ThanksReward => Target == null && Amount == 50,
            _ => false,
        };

    public string Fingerprint(string? note) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        FormattableString.Invariant($"{Kind}|{Source:D}|{Target:D}|{Amount}|{note}"))));
}

public readonly record struct PirateCurrencyResult(string State, int? Balance, int? TargetBalance = null,
    bool AppliedNow = false, int? NoteId = null);

public interface IPirateCurrencyOperations
{
    PirateCurrencyResult Apply(PirateCurrencyRequest request, AdminNote? purchasedNote = null);
    int RemoveUpTo(NetUserId player, int amount);
}
