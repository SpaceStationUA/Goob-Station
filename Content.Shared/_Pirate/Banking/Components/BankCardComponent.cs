using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Banking;

[RegisterComponent, NetworkedComponent]
public sealed partial class BankCardComponent : Component
{
    [DataField]
    public int? AccountId;

    [DataField]
    public int StartingBalance;

    [DataField]
    public bool CommandBudgetCard;

    /// <summary>The payment terminal in a station borg uses its current occupant's personal account.</summary>
    [DataField]
    public bool BindToMind;
}
