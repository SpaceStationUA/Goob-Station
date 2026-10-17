using System.Linq;
using Content.Client._Pirate.Employment.UI;
using Content.Shared._Pirate.Employment;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private readonly List<EmployerPrototype> _employers = new();
    private readonly Dictionary<string, FormattedMessage> _employerRequirementFailures = new();

    private void InitializeEmployerSelector()
    {
        EmployerButton.MinWidth = 300;
        RefreshEmployers();
        EmployerButton.OnPressed += _ => OpenEmployerSelector();
    }

    private void RefreshEmployers()
    {
        _employers.Clear();
        _employerRequirementFailures.Clear();

        var profile = Profile ?? HumanoidCharacterProfile.DefaultWithSpecies();
        var employers = _prototypeManager.EnumeratePrototypes<EmployerPrototype>()
            .OrderBy(employer => employer.Priority)
            .ThenBy(employer => employer.ID)
            .ToList();
        var failures = GetEmployerRequirementFailures(employers, profile);

        if (Profile != null &&
            (!employers.Any(employer => employer.ID == Profile.Employer) || failures.ContainsKey(Profile.Employer)))
        {
            var fallback = employers.FirstOrDefault(employer =>
                               employer.ID == SharedHumanoidAppearanceSystem.DefaultEmployer
                               && !failures.ContainsKey(employer.ID))
                           ?? employers.FirstOrDefault(employer => !failures.ContainsKey(employer.ID));
            if (fallback != null)
            {
                Profile = Profile.WithEmployer(fallback.ID);
                profile = Profile;
                SetDirty();
                failures = GetEmployerRequirementFailures(employers, profile);
            }
        }

        _employers.AddRange(employers);
        foreach (var (employer, reason) in failures)
            _employerRequirementFailures.Add(employer, reason);

        var selected = employers.FirstOrDefault(employer => employer.ID == Profile?.Employer);
        EmployerButton.Text = selected == null
            ? Loc.GetString("employment-employer-selector-choose")
            : Loc.GetString("employment-employer-profile-selected", ("employer", Loc.GetString(selected.NameKey)));
    }

    private Dictionary<string, FormattedMessage> GetEmployerRequirementFailures(
        IEnumerable<EmployerPrototype> employers,
        HumanoidCharacterProfile profile)
    {
        var failures = new Dictionary<string, FormattedMessage>();
        foreach (var employer in employers)
        {
            var reason = GetRequirementFailureReason(employer.Requirements, profile.WithEmployer(employer.ID));
            if (reason != null)
                failures.Add(employer.ID, reason);
        }

        return failures;
    }

    private void OpenEmployerSelector()
    {
        var window = new EmployerSelectionWindow(_employers, Profile?.Employer, _employerRequirementFailures);
        window.OnEmployerSelected += employer =>
        {
            SetEmployer(employer.ID);
            window.Close();
        };
        window.OpenCentered();
    }

    private void SetEmployer(string employer)
    {
        Profile = Profile?.WithEmployer(employer);
        RefreshRequirementDependentOptions();
        SetDirty();
        ReloadPreview();
    }

}
