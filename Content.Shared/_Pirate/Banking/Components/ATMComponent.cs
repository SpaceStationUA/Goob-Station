using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Banking;

[RegisterComponent]
public sealed partial class ATMComponent : Component
{
    [DataField("idCardSlot")]
    public ItemSlot CardSlot = new ();

    [DataField("currencyType")]
    public string CurrencyType = "SpaceCash";

    public string SlotId = "IdCardSlot";

    [ValidatePrototypeId<StackPrototype>]
    public string CreditStackPrototype = "Credit";

    [DataField("soundInsertCurrency")]
    public SoundSpecifier SoundInsertCurrency = new SoundPathSpecifier("/Audio/_Pirate/Machines/polaroid2.ogg");

    [DataField("soundWithdrawCurrency")]
    public SoundSpecifier SoundWithdrawCurrency = new SoundPathSpecifier("/Audio/_Pirate/Machines/polaroid1.ogg");

    [DataField("soundApply")]
    public SoundSpecifier SoundApply = new SoundPathSpecifier("/Audio/_Pirate/Machines/chime.ogg");

    [DataField("soundDeny")]
    public SoundSpecifier SoundDeny = new SoundPathSpecifier("/Audio/_Pirate/Machines/buzz-sigh.ogg");
}


[Serializable, NetSerializable]
public enum ATMUiKey
{
    Key
}
