// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CombatMode.Pacification;

namespace Content.Pirate.Server.Pacification.Systems;

/// <summary>Tracks ownership so removing the novice restriction cannot remove pre-existing pacifism.</summary>
[RegisterComponent]
public sealed partial class NovicePacificationComponent : Component
{
    public PacifiedComponent? AppliedPacification;
}
