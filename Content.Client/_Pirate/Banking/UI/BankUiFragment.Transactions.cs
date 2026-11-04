using Content.Shared._Pirate.Banking;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.Banking.UI;

public sealed partial class BankUiFragment
{
    private void UpdateTransactionHistory(BankTransaction[] transactions)
    {
        TransactionEntries.RemoveAllChildren();
        NoTransactionsLabel.Visible = transactions.Length == 0;
        TransactionsScroll.Visible = transactions.Length > 0;

        for (var i = transactions.Length - 1; i >= 0; i--)
        {
            var transaction = transactions[i];
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new Thickness(0, 0, 6, 0),
            };
            var description = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
            };
            var heading = new BoxContainer { HorizontalExpand = true };
            heading.AddChild(new Label
            {
                Text = transaction.Time.ToString(@"hh\:mm\:ss"),
                StyleClasses = { "LabelSubText" },
                Margin = new Thickness(0, 0, 8, 0),
            });
            heading.AddChild(new Label
            {
                Text = Loc.GetString(GetTransactionTypeKey(transaction.Type)),
                HorizontalExpand = true,
                ClipText = true,
            });
            description.AddChild(heading);

            var amounts = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
            };
            amounts.AddChild(new Label
            {
                Text = Loc.GetString("bank-program-ui-history-amount",
                    ("amount", transaction.Amount.ToString("+0;-0;0"))),
                Modulate = transaction.Amount > 0 ? Color.LightGreen : Color.Salmon,
                HorizontalAlignment = HAlignment.Right,
            });

            if (!string.IsNullOrEmpty(transaction.Details))
            {
                description.AddChild(new Label
                {
                    Text = transaction.Type == BankTransactionType.Transfer
                        ? Loc.GetString("bank-program-ui-history-account", ("account", transaction.Details))
                        : transaction.Details,
                    ClipText = true,
                    ToolTip = transaction.Details,
                });
            }

            amounts.AddChild(new Label
            {
                Text = Loc.GetString("atm-ui-balance", ("balance", transaction.Balance)),
                StyleClasses = { "LabelSubText" },
                HorizontalAlignment = HAlignment.Right,
            });
            row.AddChild(description);
            row.AddChild(amounts);
            TransactionEntries.AddChild(row);
        }
    }

    private static string GetTransactionTypeKey(BankTransactionType type) => type switch
    {
        BankTransactionType.Salary => "bank-transaction-salary",
        BankTransactionType.Deposit => "bank-transaction-deposit",
        BankTransactionType.Withdrawal => "bank-transaction-withdrawal",
        BankTransactionType.Purchase => "bank-transaction-purchase",
        BankTransactionType.Transfer => "bank-transaction-transfer",
        BankTransactionType.Fine => "bank-transaction-fine",
        _ => "bank-transaction-adjustment",
    };
}
