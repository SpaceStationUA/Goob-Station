using Content.Shared.DoAfter;
using Content.Shared._DV.CartridgeLoader.Cartridges;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.NanoChat;

[Serializable, NetSerializable]
public sealed partial class PdaPhotoPrintToFaxDoAfterEvent(NetEntity cardUid, NanoChatPhotoData photo) : SimpleDoAfterEvent
{
    public NetEntity CardUid = cardUid;
    public NanoChatPhotoData Photo = photo;

    public PdaPhotoPrintToFaxDoAfterEvent() : this(NetEntity.Invalid, default)
    {
    }

    public override DoAfterEvent Clone() => new PdaPhotoPrintToFaxDoAfterEvent(CardUid, Photo);
}
