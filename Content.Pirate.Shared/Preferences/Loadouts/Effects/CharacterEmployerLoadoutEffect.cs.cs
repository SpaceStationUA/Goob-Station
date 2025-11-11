using Content.Shared.Preferences;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Player;

namespace Content.Shared.Preferences.Loadouts.Effects;

[DataDefinition]
public sealed partial class CharacterEmployerRequirement : LoadoutEffect
{
    [DataField(required: true)]
    public List<string> Employers = new();

    public override bool Validate(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        ICommonSession? session,
        IDependencyCollection collection,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = null;

        if (profile.Employer == string.Empty || !Employers.Contains(profile.Employer))
        {
            reason = FormattedMessage.FromMarkup($"Вимагається один із роботодавців: {string.Join(", ", Employers)}.");
            return false;
        }

        return true;
    }

    public override void Apply(RoleLoadout loadout) {}
}