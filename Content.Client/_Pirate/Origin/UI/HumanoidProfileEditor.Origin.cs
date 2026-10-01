using System.Linq;
using Content.Shared._Pirate.Origin;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private readonly List<CitizenshipPrototype> _citizenships = new();
    private bool _suppressCitizenshipSelector;

    private void InitializeCitizenshipSelector()
    {
        RefreshCitizenships();

        CitizenshipButton.OnItemSelected += args =>
        {
            if (_suppressCitizenshipSelector)
                return;

            CitizenshipButton.SelectId(args.Id);
            SetCitizenship(_citizenships[args.Id].ID);
        };
    }

    private void RefreshCitizenships()
    {
        CitizenshipButton.Clear();
        _citizenships.Clear();

        var profile = Profile ?? HumanoidCharacterProfile.DefaultWithSpecies();
        var available = _prototypeManager.EnumeratePrototypes<CitizenshipPrototype>()
            .Where(citizenship => RequirementsValid(citizenship.Requirements, profile.WithCitizenship(citizenship.ID)))
            .OrderBy(citizenship => Loc.GetString(citizenship.NameKey))
            .ToList();

        if (Profile != null && !available.Any(citizenship => citizenship.ID == Profile.Citizenship))
        {
            var fallback = available.FirstOrDefault(citizenship => citizenship.ID == SharedHumanoidAppearanceSystem.DefaultCitizenship)
                           ?? available.FirstOrDefault();
            if (fallback != null)
            {
                Profile = Profile.WithCitizenship(fallback.ID);
                SetDirty();
            }
        }

        _citizenships.AddRange(available);
        _suppressCitizenshipSelector = true;
        try
        {
            var selectedIndex = -1;
            for (var i = 0; i < _citizenships.Count; i++)
            {
                CitizenshipButton.AddItem(Loc.GetString(_citizenships[i].NameKey), i);
                if (selectedIndex < 0 && Profile?.Citizenship == _citizenships[i].ID)
                    selectedIndex = i;
            }

            if (selectedIndex >= 0)
                CitizenshipButton.SelectId(selectedIndex);
        }
        finally
        {
            _suppressCitizenshipSelector = false;
        }
    }

    private void SetCitizenship(string citizenship)
    {
        Profile = Profile?.WithCitizenship(citizenship);
        RefreshRequirementDependentOptions();
        SetDirty();
        ReloadPreview();
    }
}
