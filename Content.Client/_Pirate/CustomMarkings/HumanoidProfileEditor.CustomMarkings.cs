// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Pirate.CustomMarkings.UI;
using Content.Shared._Pirate.CustomMarkings;

namespace Content.Client.Lobby.UI;

/// <summary>
/// The Custom markings card on the Markings tab: the player's markings as tiles to put on and take off, and the
/// button that opens their library.
/// </summary>
public sealed partial class HumanoidProfileEditor
{
    private CustomMarkingLibraryWindow? _customMarkingWindow;
    private CustomMarkingQuickList? _customMarkingList;

    private void InitializeCustomMarkings()
    {
        CustomMarkingsButton.OnPressed += _ => OpenCustomMarkings();
        _customMarkingList = new CustomMarkingQuickList();
        _customMarkingList.OnWornChanged += SetCustomMarkings;
        CustomMarkingsBody.AddChild(_customMarkingList);
        UpdateCustomMarkings();
    }

    /// <summary>Syncs the button with the loaded profile and closes a library opened for the previous one.</summary>
    private void UpdateCustomMarkings()
    {
        CloseCustomMarkings();
        CustomMarkingsCard.Visible = _cfgManager.GetCVar(CustomMarkingCVars.Enabled);
        UpdateCustomMarkingsButton();
        _customMarkingList?.SetWorn(Profile?.CustomMarkings ?? new List<CustomMarking>());
    }

    /// <summary>Turns the tiles to face the way the preview does.</summary>
    private void SetCustomMarkingsDirection(Direction direction)
    {
        _customMarkingList?.SetDirection(direction);
    }

    private void UpdateCustomMarkingsButton()
    {
        CustomMarkingsButton.Text = Loc.GetString("wf-custom-marking-creator-button", ("worn", Profile?.CustomMarkings.Count ?? 0));
    }

    private void OpenCustomMarkings()
    {
        CloseCustomMarkings();
        if (Profile == null)
            return;

        _customMarkingWindow = new CustomMarkingLibraryWindow(() => Profile);
        _customMarkingWindow.SetWorn(Profile.CustomMarkings);
        _customMarkingWindow.OnWornChanged += SetCustomMarkings;
        _customMarkingWindow.OnClose += () => _customMarkingWindow = null;
        _customMarkingWindow.OpenCentered();
    }

    private void SetCustomMarkings(List<CustomMarking> worn)
    {
        if (Profile == null)
            return;

        Profile = Profile.WithCustomMarkings(worn);
        UpdateCustomMarkingsButton();
        // Whichever of the two the change came from, the other shows it.
        _customMarkingList?.SetWorn(worn);
        _customMarkingWindow?.SetWorn(worn);
        ReloadProfilePreview();
    }

    private void CloseCustomMarkings()
    {
        _customMarkingWindow?.Close();
        _customMarkingWindow = null;
    }
}
