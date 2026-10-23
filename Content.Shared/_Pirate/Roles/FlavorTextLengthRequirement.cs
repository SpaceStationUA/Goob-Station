// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Pirate.CCVars;
using UpstreamCVars = Content.Shared.CCVar.CCVars;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Roles;

[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class FlavorTextLengthRequirement : JobRequirement
{
    [DataField]
    public int? MinLength;

    public override bool Check(IEntityManager entManager,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = null;

        var cfg = IoCManager.Resolve<IConfigurationManager>();
        var minLength = MinLength ?? cfg.GetCVar(PirateVars.CommandFlavorTextMinLength);

        // Flavor text must be enabled before players can write a description.
        if (minLength <= 0 || !cfg.GetCVar(UpstreamCVars.FlavorText))
            return true;

        // Character pods can reach this check without a profile.
        var length = profile?.PhysicalDescription.Trim().Length ?? 0;
        var met = length >= minLength;
        if (Inverted)
            met = !met;

        if (met)
            return true;

        reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
            Inverted ? "role-timer-flavor-text-too-long" : "role-timer-flavor-text-too-short",
            ("required", minLength),
            ("current", length)));
        return false;
    }
}
