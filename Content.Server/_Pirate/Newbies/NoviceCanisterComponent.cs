// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Player;

namespace Content.Server._Pirate.Newbies;

/// <summary>Remembers who opened a protected canister, including after they disconnect.</summary>
[RegisterComponent]
public sealed partial class NoviceCanisterComponent : Component
{
    public ICommonSession? Opener;
}

[RegisterComponent]
public sealed partial class NoviceCanisterAlertComponent : Component
{
    public TimeSpan NextAlert;
}
