using Content.Shared._Pirate.Banking;

namespace Content.Server._Pirate.Banking;

public sealed partial class BankCardSystem
{
    private void RecordTransaction(BankAccount account, int amount, BankTransactionType type, string details = "")
    {
        if (amount == 0 || type == BankTransactionType.OpeningBalance)
            return;

        var time = _gameTicker.RoundDuration();
        account.Transactions.Add(new BankTransaction(
            time < TimeSpan.Zero ? TimeSpan.Zero : time, amount, account.Balance, type, details));
    }

    public bool TryTransferBalance(int senderId, int recipientId, int amount)
    {
        if (amount <= 0 || senderId == recipientId ||
            !TryGetAccount(senderId, out var sender) || !TryGetAccount(recipientId, out var recipient) ||
            sender.Balance < amount || (long) recipient.Balance + amount > int.MaxValue)
            return false;

        TryChangeBalance(senderId, -amount, BankTransactionType.Transfer, recipientId.ToString());
        TryChangeBalance(recipientId, amount, BankTransactionType.Transfer, senderId.ToString());
        return true;
    }
}
