using Content.Shared._Pirate.Employment;
using Content.Shared._Pirate.Origin;
using Content.Shared.Humanoid;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    [DataField("nationality")]
    public string Citizenship { get; set; } = SharedHumanoidAppearanceSystem.DefaultCitizenship;

    public HumanoidCharacterProfile WithCitizenship(string citizenship)
    {
        return new(this) { Citizenship = citizenship };
    }

    private bool CitizenshipEquals(HumanoidCharacterProfile other) => Citizenship == other.Citizenship;

    private void AddCitizenshipHash(ref HashCode hashCode) => hashCode.Add(Citizenship);

    private void EnsureCitizenshipValid(IPrototypeManager prototypes, int maxLength)
    {
        Citizenship = ValidateText(Citizenship, maxLength);

        if (!prototypes.HasIndex<CitizenshipPrototype>(Citizenship))
            Citizenship = SharedHumanoidAppearanceSystem.DefaultCitizenship;
    }

    private void EnsurePirateProfileRequirementsValid(ICommonSession session, IDependencyCollection collection)
    {
        var prototypes = collection.Resolve<IPrototypeManager>();
        var entityManager = collection.Resolve<IEntityManager>();
        var playTimes = collection.Resolve<ISharedPlaytimeManager>().GetPlayTimes(session);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var citizenshipValid = MeetsRequirements(
                prototypes.Index<CitizenshipPrototype>(Citizenship).Requirements,
                this,
                entityManager,
                prototypes,
                playTimes);
            var employerValid = MeetsRequirements(
                prototypes.Index<EmployerPrototype>(Employer).Requirements,
                this,
                entityManager,
                prototypes,
                playTimes);

            if (citizenshipValid && employerValid)
                break;

            if (!citizenshipValid)
                Citizenship = SharedHumanoidAppearanceSystem.DefaultCitizenship;

            if (!employerValid)
                Employer = SharedHumanoidAppearanceSystem.DefaultEmployer;
        }

        if (!MeetsRequirements(
                prototypes.Index<CitizenshipPrototype>(Citizenship).Requirements,
                this,
                entityManager,
                prototypes,
                playTimes)
            || !MeetsRequirements(
                prototypes.Index<EmployerPrototype>(Employer).Requirements,
                this,
                entityManager,
                prototypes,
                playTimes))
        {
            Citizenship = SharedHumanoidAppearanceSystem.DefaultCitizenship;
            Employer = SharedHumanoidAppearanceSystem.DefaultEmployer;
        }

    }

    private static bool MeetsRequirements(
        IEnumerable<JobRequirement> requirements,
        HumanoidCharacterProfile profile,
        IEntityManager entityManager,
        IPrototypeManager prototypes,
        IReadOnlyDictionary<string, TimeSpan> playTimes)
    {
        foreach (var requirement in requirements)
        {
            if (!requirement.Check(entityManager, prototypes, profile, playTimes, out _))
                return false;
        }

        return true;
    }

    private static string ValidateText(string? value, int maxLength)
    {
        var clean = FormattedMessage.RemoveMarkupOrThrow(value ?? string.Empty);
        return clean.Length > maxLength ? clean[..maxLength] : clean;
    }
}
