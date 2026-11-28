// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

namespace Content.Shared._Pirate.Suspicion.SuspicionGameRule.Components;

[RegisterComponent]
public sealed partial class SuspicionRadarComponent : Component
{
    [DataField]
    public bool ShowTraitors { get; set; } = true;
}
