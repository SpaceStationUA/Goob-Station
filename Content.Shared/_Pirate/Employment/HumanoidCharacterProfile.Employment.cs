using Content.Shared._Pirate.Employment;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    [DataField]
    public string Employer { get; set; } = SharedHumanoidAppearanceSystem.DefaultEmployer;

    public HumanoidCharacterProfile WithEmployer(string employer)
    {
        return new(this) { Employer = employer };
    }

    private bool EmployerEquals(HumanoidCharacterProfile other) => Employer == other.Employer;

    private void AddEmployerHash(ref HashCode hashCode) => hashCode.Add(Employer);

    private void EnsureEmployerValid(IPrototypeManager prototypes, int maxLength)
    {
        Employer = FormattedMessage.RemoveMarkupOrThrow(Employer ?? string.Empty);
        if (Employer.Length > maxLength)
            Employer = Employer[..maxLength];

        if (!prototypes.HasIndex<EmployerPrototype>(Employer))
            Employer = SharedHumanoidAppearanceSystem.DefaultEmployer;
    }
}
