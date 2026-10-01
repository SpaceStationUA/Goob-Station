using Content.Shared._Pirate.Origin;
using Content.Shared.Humanoid;
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

    private static string ValidateText(string? value, int maxLength)
    {
        var clean = FormattedMessage.RemoveMarkupOrThrow(value ?? string.Empty);
        return clean.Length > maxLength ? clean[..maxLength] : clean;
    }
}
