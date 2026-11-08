// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Server.GameTicking.Rules.Components;

// Antags without a job do not receive an account or salary.
[RegisterComponent]
public sealed partial class AntagStartingBalanceRuleComponent : Component
{
    [DataField(required: true)]
    public int Balance;
}
