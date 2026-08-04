// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Server.ServerCurrency;

/// <summary>Tracks a cryopod stay for the early-cryo penalty.</summary>
[RegisterComponent]
public sealed partial class PirateCryoEntryTimeComponent : Component
{
    [ViewVariables]
    public TimeSpan RoundTimeOnEntry;

    [ViewVariables]
    public bool Charged;
}
