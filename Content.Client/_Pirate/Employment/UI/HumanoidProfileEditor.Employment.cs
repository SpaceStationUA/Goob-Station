using System.Linq;
using Content.Client._Pirate.Employment.UI;
using Content.Shared._Pirate.Employment;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private readonly List<EmployerPrototype> _employers = new();

    private void InitializeEmployerSelector()
    {
        EmployerButton.MinWidth = 300;
        RefreshEmployers();
        EmployerButton.OnPressed += _ => OpenEmployerSelector();
    }

    private void RefreshEmployers()
    {
        _employers.Clear();
        var profile = Profile ?? HumanoidCharacterProfile.DefaultWithSpecies();
        var available = GetAvailableEmployers(profile);

        if (Profile != null && !available.Any(employer => employer.ID == Profile.Employer))
        {
            var fallback = available.FirstOrDefault(employer => employer.ID == SharedHumanoidAppearanceSystem.DefaultEmployer)
                           ?? available.FirstOrDefault();
            if (fallback != null)
            {
                Profile = Profile.WithEmployer(fallback.ID);
                profile = Profile;
                SetDirty();
                available = GetAvailableEmployers(profile);
            }
        }

        _employers.AddRange(available);
        var selected = _employers.FirstOrDefault(employer => employer.ID == Profile?.Employer);
        EmployerButton.Text = selected == null
            ? Loc.GetString("employment-employer-selector-choose")
            : Loc.GetString("employment-employer-selector-selected", ("employer", Loc.GetString(selected.NameKey)));
    }

    private List<EmployerPrototype> GetAvailableEmployers(HumanoidCharacterProfile profile)
    {
        return _prototypeManager.EnumeratePrototypes<EmployerPrototype>()
            .Where(employer => RequirementsValid(employer.Requirements, profile.WithEmployer(employer.ID)))
            .OrderBy(employer => employer.Priority)
            .ThenBy(employer => employer.ID)
            .ToList();
    }

    private void OpenEmployerSelector()
    {
        var window = new EmployerSelectionWindow(_employers, Profile?.Employer);
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
