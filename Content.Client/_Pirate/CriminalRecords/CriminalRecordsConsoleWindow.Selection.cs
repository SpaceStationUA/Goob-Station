// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Pirate.StationRecords;
using Content.Shared.CriminalRecords;
using Content.Shared.StationRecords;
using Robust.Shared.Enums;

namespace Content.Client.CriminalRecords;

public sealed partial class CriminalRecordsConsoleWindow
{
    private RecordSelectionController<CriminalRecordsConsoleState> _selection = default!;
    private RecordFieldWidths _fieldWidths = default!;

    private uint? _identityEditorKey;
    private (string Species, string Nationality, string Employer, Gender Gender, int Age, string? RawSpecies, string? RawNationality, string? RawEmployer, int RawAge) _identityEditorSource;

    private void InitRecordSelection()
    {
        _selection = new RecordSelectionController<CriminalRecordsConsoleState>(RecordListing);
        _selection.SelectionRequested += key => OnKeySelected?.Invoke(key);
        _selection.ShowCached += (cached, last) =>
            UpdateState(new CriminalRecordsConsoleState(last.RecordListing, null)
            {
                SelectedKey = cached.SelectedKey,
                StationRecord = cached.StationRecord,
                CriminalRecord = cached.CriminalRecord,
                FilterStatus = last.FilterStatus,
            });
    }

    private bool AcceptServerState(CriminalRecordsConsoleState state)
    {
        if (_selection.AcceptServerState(state, state.SelectedKey, state is { StationRecord: not null, CriminalRecord: not null }))
            return true;

        NoRecords.Visible = state.RecordListing == null || state.RecordListing.Count == 0;
        PopulateRecordListing(state.RecordListing);
        return false;
    }

    private void RefreshRecordIdentityEditor(uint key, GeneralStationRecord stationRecord, CriminalRecord criminalRecord)
    {
        var profile = BuildRecordIdentityProfile(stationRecord, criminalRecord);
        var source = (profile.Species, profile.Nationality, profile.Employer, profile.Gender, profile.Age,
            stationRecord.Species, stationRecord.Nationality, stationRecord.Employer, stationRecord.Age);

        if (_identityEditorKey == key && _identityEditorSource == source)
            return;

        UpdateRecordIdentityProfile(profile, false, stationRecord);
        _identityEditorKey = key;
        _identityEditorSource = source;
    }

    private void InitFieldWidths()
    {
        _fieldWidths = new RecordFieldWidths(
            [SpeciesButton, NationalityButton, EmployerButton, GenderButton, StatusOptionButton],
            [AgeEdit],
            [PersonJob.Parent!]);
        _fieldWidths.Update();
    }
}
