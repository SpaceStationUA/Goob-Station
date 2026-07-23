// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Cyberdeck;

public sealed partial class CyberdeckHackActionEvent : EntityTargetActionEvent;

public sealed partial class CyberdeckVisionEvent : InstantActionEvent;

public sealed partial class CyberdeckVisionReturnEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class CyberdeckHackDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class CyberdeckInfoAlertEvent : BaseAlertEvent;

[ByRefEvent]
public record struct CyberdeckHackDeviceEvent(EntityUid User, bool Refund = false);

[ByRefEvent]
public record struct BeforeCyberdeckHackEvent(
    EntityUid OriginalTarget,
    EntityUid Target,
    TimeSpan PenaltyTime,
    bool Handled);

[ByRefEvent]
public record struct AfterCyberdeckHackEvent(EntityUid OriginalTarget, EntityUid Target, bool Handled);
