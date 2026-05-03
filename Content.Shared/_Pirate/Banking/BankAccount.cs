using Content.Shared.Mind;
using Robust.Shared.Serialization;
using Robust.Shared.Map;

namespace Content.Shared._Pirate.Banking;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class BankAccount
{
    [DataField]
    public int AccountId;
    [DataField]
    public int AccountPin;
    [DataField]
    public int Balance;
    [DataField]
    public bool CommandBudgetAccount;
    [DataField]
    public NetEntity? Mind;
    [DataField]
    public string Name = string.Empty;

    [DataField]
    public NetEntity? CartridgeUid;

    public BankAccount(int accountId, int balance)
    {
        AccountId = accountId;
        Balance = balance;
        AccountPin = System.Random.Shared.Next(1000, 10000);
    }
}
