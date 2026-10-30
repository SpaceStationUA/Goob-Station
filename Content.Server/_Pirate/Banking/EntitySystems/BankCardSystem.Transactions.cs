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
            sender.Arrested || sender.Balance < amount || (long) recipient.Balance + amount > int.MaxValue)
            return false;

        TryChangeBalance(senderId, -amount, BankTransactionType.Transfer, recipientId.ToString());
        TryChangeBalance(recipientId, amount, BankTransactionType.Transfer, senderId.ToString());
        return true;
    }

    private static bool IsSpending(BankTransactionType type)
    {
        return type is not (BankTransactionType.Withdrawal or BankTransactionType.Fine
            or BankTransactionType.Adjustment or BankTransactionType.OpeningBalance);
    }

    public bool IsArrested(int accountId)
    {
        return TryGetAccount(accountId, out var account) && account.Arrested;
    }

    public void SetArrested(int accountId, bool arrested)
    {
        if (!TryGetAccount(accountId, out var account) || account.Arrested == arrested)
            return;

        account.Arrested = arrested;
        if (account.CartridgeUid != null)
            _bankCartridge.UpdateUiState(GetEntity(account.CartridgeUid.Value));
    }

    public IEnumerable<int> GetAccountIdsByName(string name)
    {
        foreach (var account in _accounts)
        {
            if (!account.CommandBudgetAccount && account.Name == name)
                yield return account.AccountId;
        }
    }
}
