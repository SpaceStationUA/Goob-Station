// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Content.Shared._DV.CartridgeLoader.Cartridges;
using Robust.Shared.Serialization;

namespace Content.Shared._DV.NanoChat;

[Serializable, NetSerializable]
public sealed partial class PdaPhotoUploadDoAfterEvent(NetEntity cardUid, NanoChatPhotoData photo) : SimpleDoAfterEvent
{
    public NetEntity CardUid = cardUid;
    public NanoChatPhotoData Photo = photo;

    public PdaPhotoUploadDoAfterEvent() : this(NetEntity.Invalid, default)
    {
    }

    public override DoAfterEvent Clone() => new PdaPhotoUploadDoAfterEvent(CardUid, Photo);
}
