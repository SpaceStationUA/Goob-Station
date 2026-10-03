using System.Linq;
using Content.Shared.Customization.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void InitializePirateProfileSelectors()
    {
        PreferenceUnavailableButton.MinWidth = 300;
        InitializeCitizenshipSelector();
        InitializeEmployerSelector();
    }

    private void RefreshRequirementDependentOptions()
    {
        for (var i = 0; i < 3; i++)
        {
            var citizenship = Profile?.Citizenship;
            var employer = Profile?.Employer;
            RefreshCitizenships();
            RefreshEmployers();
            if (citizenship == Profile?.Citizenship && employer == Profile?.Employer)
                break;
        }

        RefreshJobs();
        RefreshLoadouts();
        RefreshTraits();
    }

    private bool RequirementsValid(IReadOnlyCollection<JobRequirement>? requirements, HumanoidCharacterProfile profile)
    {
        if (requirements == null || requirements.Count == 0)
            return true;

        var session = _playerManager.LocalSession;
        var playTimes = session == null ? new Dictionary<string, TimeSpan>() : _requirements.GetPlayTimes(session);
        return requirements.All(requirement => requirement.Check(_entManager, _prototypeManager, profile, playTimes, out _));
    }
}
