// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Voodoo;

[Serializable, NetSerializable]
public sealed partial class VoodooUnbindDoAfterEvent : SimpleDoAfterEvent;
