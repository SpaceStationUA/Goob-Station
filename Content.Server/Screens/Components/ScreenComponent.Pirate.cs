// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server.Screens.Components;

public sealed partial class ScreenComponent
{
    /// <summary>Ignore broadcast screen packets; used by signal-driven screens.</summary>
    [DataField]
    public bool IgnoreNetwork;
}
