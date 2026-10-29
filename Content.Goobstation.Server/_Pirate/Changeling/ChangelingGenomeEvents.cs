// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Changeling.Components;

namespace Content.Goobstation.Server.Changeling;

[ByRefEvent]
public readonly record struct ChangelingDnaStolenEvent(EntityUid Victim, TransformData Data);

[ByRefEvent]
public readonly record struct ChangelingFormAppliedEvent(TransformData Data);
