using Content.Shared._Pirate.Banking;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Banking.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class StationBankComponent : Component
{
    [DataField]
    public BankAccount BankAccount = new(0, 0);
}
