// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences.Loadouts.Effects;

/// <summary>
/// Restricts a personal loadout reward to an account name, independently of the character's name.
/// </summary>
public sealed partial class UsernameLoadoutRequirementEffect : LoadoutEffect
{
    [DataField(required: true)]
    public string Username = string.Empty;

    [DataField(required: true)]
    public LocId FailureReason;

    public override bool Validate(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        ICommonSession? session,
        IDependencyCollection collection,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        if (session != null && string.Equals(session.Name, Username, StringComparison.OrdinalIgnoreCase))
        {
            reason = null;
            return true;
        }

        reason = FormattedMessage.FromUnformatted(Loc.GetString(FailureReason));
        return false;
    }
}
