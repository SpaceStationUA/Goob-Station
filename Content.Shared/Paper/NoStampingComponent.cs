// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Robust.Shared.GameStates;

namespace Content.Shared.Paper;

/// <summary>
/// Blocks stamping this paper-like entity.
/// Used by diaries: a single stamp would make the paper uneditable forever,
/// which would permanently lock a persistent diary.
/// </summary>
[RegisterComponent]
[NetworkedComponent]
public sealed partial class NoStampingComponent : Component
{
}
