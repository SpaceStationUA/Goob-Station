// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Roles;

[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class SpeciesJobWhitelistRequirement : JobRequirement
{
    [DataField(required: true)]
    public ProtoId<JobPrototype> Job;

    public override bool Check(IEntityManager entManager,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = null;

        // Ghost roles may have no character profile.
        if (profile is null)
            return true;

        HashSet<ProtoId<JobPrototype>>? allowed = null;
        foreach (var whitelist in protoManager.EnumeratePrototypes<SpeciesJobWhitelistPrototype>())
        {
            if (whitelist.Species != profile.Species)
                continue;

            allowed ??= new HashSet<ProtoId<JobPrototype>>();
            allowed.UnionWith(whitelist.Jobs);
        }

        var met = allowed == null || allowed.Contains(Job);
        if (Inverted)
            met = !met;

        if (met)
            return true;

        var species = protoManager.TryIndex(profile.Species, out var speciesProto)
            ? Loc.GetString(speciesProto.Name)
            : profile.Species.Id;

        var jobs = allowed == null
            ? string.Empty
            : string.Join(", ", allowed
                .Select(j => protoManager.TryIndex(j, out var job) ? job.LocalizedName : j.Id)
                .OrderBy(n => n));

        reason = FormattedMessage.FromMarkupPermissive(Loc.GetString("role-timer-species-job-whitelist",
            ("species", species),
            ("jobs", jobs)));
        return false;
    }
}
