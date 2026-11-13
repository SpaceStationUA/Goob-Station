// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Pirate.StationRecords;
using Content.Shared.CriminalRecords;
using Content.Shared.Preferences;
using Content.Shared.StationRecords;
using Robust.Shared.Enums;

namespace Content.Client.CriminalRecords;

public sealed partial class CriminalRecordsConsoleWindow
{
    private RecordSelectionController<CriminalRecordsConsoleState> _selection = default!;

    public uint SelectionRequest => _selection.LastRequest;
    private RecordFieldWidths _fieldWidths = default!;

    private uint? _identityEditorKey;
    private (string Species, string Citizenship, string Employer, Gender Gender, int Age, string? RawSpecies, string? RawCitizenship, string? RawEmployer, int RawAge) _identityEditorSource;

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
                Portrait = cached.Portrait,
                Log = cached.Log,
                FilterStatus = last.FilterStatus,
            });
    }

    private bool AcceptServerState(CriminalRecordsConsoleState state)
    {
        if (_selection.AcceptServerState(state, state.SelectedKey, state.SelectionRequest, state is { StationRecord: not null, CriminalRecord: not null }))
            return true;

        NoRecords.Visible = state.RecordListing == null || state.RecordListing.Count == 0;
        PopulateRecordListing(state.RecordListing);
        return false;
    }

    private void RefreshRecordIdentityEditor(uint key, GeneralStationRecord stationRecord, HumanoidCharacterProfile? snapshot)
    {
        var profile = BuildRecordIdentityProfile(stationRecord, snapshot);
        var source = (profile.Species, profile.Citizenship, profile.Employer, profile.Gender, profile.Age,
            stationRecord.Species, stationRecord.Citizenship, stationRecord.Employer, stationRecord.Age);

        if (_identityEditorKey == key && _identityEditorSource == source)
            return;

        UpdateRecordIdentityProfile(profile, false, stationRecord);
        _identityEditorKey = key;
        _identityEditorSource = source;
    }

    public event Action<string>? OnAddLogNote;
    public event Action<uint>? OnDeleteLogEntry;
    public event Action? OnPrintLog;
    private RecordTabs _tabs = default!;

    private void InitTabs()
    {
        _tabs = new RecordTabs(ProfileTabButton, HistoryTabButton, ProfileView, HistoryView);
        _tabs.BindSearch(FilterType, FilterText, HistoryView);
        HistoryView.OnAddNote += text => OnAddLogNote?.Invoke(text);
        HistoryView.OnDeleteEntry += id => OnDeleteLogEntry?.Invoke(id);
        HistoryView.OnPrint += () => OnPrintLog?.Invoke();
        InitCitations();
    }

    private void InitFieldWidths()
    {
        _fieldWidths = new RecordFieldWidths(
            [SpeciesButton, CitizenshipButton, EmployerButton, GenderButton, StatusOptionButton],
            [AgeEdit],
            [PersonJob.Parent!]);
        _fieldWidths.Update();
    }
}
