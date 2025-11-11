using Content.Shared.Preferences;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Player;

namespace Content.Shared.Preferences.Loadouts.Effects;

[DataDefinition]
public sealed partial class CharacterNationalityRequirement : LoadoutEffect
{
    [DataField(required: true)]
    public List<string> Nationalities = new();

    public override bool Validate(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        ICommonSession? session,
        IDependencyCollection collection,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = null;

        if (profile.Nationality == string.Empty || !Nationalities.Contains(profile.Nationality))
        {
            reason = FormattedMessage.FromMarkup($"Вимагається одна з націй: {string.Join(", ", Nationalities)}.");
            return false;
        }

        return true;
    }

    public override void Apply(RoleLoadout loadout) {}
}