using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Utility;

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

    private bool AreRequirementsMet(
        IReadOnlyCollection<JobRequirement>? requirements,
        HumanoidCharacterProfile profile)
    {
        return GetRequirementFailureReason(requirements, profile) == null;
    }

    private FormattedMessage? GetRequirementFailureReason(
        IReadOnlyCollection<JobRequirement>? requirements,
        HumanoidCharacterProfile profile)
    {
        if (requirements == null || requirements.Count == 0)
            return null;

        var session = _playerManager.LocalSession;
        var playTimes = session == null ? new Dictionary<string, TimeSpan>() : _requirements.GetPlayTimes(session);
        var reasons = new List<string>();
        foreach (var requirement in requirements)
        {
            if (requirement.Check(_entManager, _prototypeManager, profile, playTimes, out var reason))
                continue;

            reasons.Add(reason.ToMarkup());
        }

        return reasons.Count == 0 ? null : FormattedMessage.FromMarkupOrThrow(string.Join('\n', reasons));
    }
}
