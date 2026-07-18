// SPDX-FileCopyrightText: 2026 kotobdev <59124164+kotobdev@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later OR MIT

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Avali.Events;

[Serializable, NetSerializable]
public sealed partial class PreeningEvent : SimpleDoAfterEvent;
