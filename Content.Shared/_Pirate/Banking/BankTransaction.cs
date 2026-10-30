using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Banking;

[Serializable, NetSerializable]
public enum BankTransactionType : byte
{
    Adjustment,
    OpeningBalance,
    Salary,
    Deposit,
    Withdrawal,
    Purchase,
    Transfer,
    Fine,
}

[Serializable, NetSerializable]
public sealed record BankTransaction(
    TimeSpan Time,
    int Amount,
    int Balance,
    BankTransactionType Type,
    string Details);
