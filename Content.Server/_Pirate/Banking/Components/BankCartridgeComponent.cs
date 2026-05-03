namespace Content.Server._Pirate.Banking;

[RegisterComponent]
public sealed partial class BankCartridgeComponent : Component
{
    [ViewVariables]
    public int? AccountId;

    [ViewVariables]
    public EntityUid? Loader;

    public string AccountLinkResult = string.Empty;
}
