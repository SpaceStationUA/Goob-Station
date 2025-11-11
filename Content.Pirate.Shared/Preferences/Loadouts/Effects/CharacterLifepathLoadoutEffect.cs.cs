using Content.Shared.Preferences;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Player;

namespace Content.Shared.Preferences.Loadouts.Effects;

[DataDefinition]
public sealed partial class CharacterLifepathRequirement : LoadoutEffect
{
    [DataField(required: true)]
    public List<string> Lifepaths = new();

    public override bool Validate(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        ICommonSession? session,
        IDependencyCollection collection,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = null;

        if (profile.Lifepath == string.Empty || !Lifepaths.Contains(profile.Lifepath))
        {
            reason = FormattedMessage.FromMarkup($"Вимагається один із життєвих шляхів: {string.Join(", ", Lifepaths)}.");
            return false;
        }

        return true;
    }

    public override void Apply(RoleLoadout loadout) {}
}