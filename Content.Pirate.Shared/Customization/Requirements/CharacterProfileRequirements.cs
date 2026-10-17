using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Pirate.Employment;
using Content.Shared._Pirate.Origin;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Customization.Systems;

// --- 1. CharacterEmployerRequirement ---
[UsedImplicitly, Serializable, NetSerializable]
public sealed partial class CharacterEmployerRequirement : JobRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<EmployerPrototype>> Employers;

    public override bool Check(
        IEntityManager entMan,
        IPrototypeManager protMan,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        if (profile == null)
        {
            reason = FormattedMessage.FromUnformatted(Loc.GetString("requirement-character-profile-not-found"));
            return Inverted;
        }

        var employerNames = Employers
            .Select(id => protMan.TryIndex(id, out EmployerPrototype? employer)
                ? Loc.GetString(employer.NameKey)
                : id.Id)
            .OrderBy(name => name)
            .ToList();
        reason = CharacterRequirementFormatting.Format(
            "character-employer-requirement",
            "employers",
            employerNames,
            Inverted);

        var isValid = !string.IsNullOrEmpty(profile.Employer)
                      && Employers.Any(employer => employer == profile.Employer);
        return Inverted ? !isValid : isValid;
    }
}

// --- 2. CharacterCitizenshipRequirement ---
[UsedImplicitly, Serializable, NetSerializable]
public sealed partial class CharacterCitizenshipRequirement : JobRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<CitizenshipPrototype>> Citizenships;

    public override bool Check(
        IEntityManager entMan,
        IPrototypeManager protMan,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        if (profile == null)
        {
            reason = FormattedMessage.FromUnformatted(Loc.GetString("requirement-character-profile-not-found"));
            return Inverted;
        }

        var citizenshipNames = Citizenships
            .Select(id => protMan.TryIndex(id, out CitizenshipPrototype? citizenship)
                ? Loc.GetString(citizenship.NameKey)
                : id.Id)
            .OrderBy(name => name)
            .ToList();
        reason = CharacterRequirementFormatting.Format(
            "character-citizenship-requirement",
            "citizenships",
            citizenshipNames,
            Inverted);

        var isValid = !string.IsNullOrEmpty(profile.Citizenship)
                      && Citizenships.Any(citizenship => citizenship == profile.Citizenship);
        return Inverted ? !isValid : isValid;
    }
}

internal static class CharacterRequirementFormatting
{
    public static FormattedMessage Format(string key, string field, IReadOnlyCollection<string> values, bool inverted)
    {
        const string color = "green";
        var formattedValues = $"[color={color}]{string.Join($"[/color], [color={color}]", values)}[/color]";
        return FormattedMessage.FromMarkup(Loc.GetString(key, ("inverted", inverted), (field, formattedValues)));
    }
}
