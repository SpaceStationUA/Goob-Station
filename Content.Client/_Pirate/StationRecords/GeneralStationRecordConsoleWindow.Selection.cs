// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Pirate.StationRecords;
using Content.Shared.StationRecords;

namespace Content.Client.StationRecords;

public sealed partial class GeneralStationRecordConsoleWindow
{
    private RecordSelectionController<GeneralStationRecordConsoleState> _selection = default!;

    private void InitRecordSelection()
    {
        _selection = new RecordSelectionController<GeneralStationRecordConsoleState>(RecordListing);
        _selection.SelectionRequested += key => OnKeySelected?.Invoke(key);
        _selection.ShowCached += (cached, last) =>
            UpdateState(new GeneralStationRecordConsoleState(
                cached.SelectedKey,
                cached.Record,
                last.RecordListing,
                null,
                last.CanDeleteEntries,
                last.MaxStringLength)
            {
                Portrait = cached.Portrait,
            });
    }

    public event Action<uint, byte[]?>? OnPrintPortrait;
    public event Action? OnUploadPortrait;
    public event Action<uint, byte[]>? OnStoreGeneratedPortrait;

    private void ShowRecordPortrait(GeneralStationRecordConsoleState state)
    {
        if (state is not { SelectedKey: { } key, Record: { } record }
            || RecordContainer.ChildCount == 0
            || RecordContainer.GetChild(0) is not GeneralRecord panel)
        {
            return;
        }

        panel.SetPortrait(key, state.Portrait, record, _access && _canManageRecords);
    }

    private bool AcceptServerState(GeneralStationRecordConsoleState state)
    {
        if (_selection.AcceptServerState(state, state.SelectedKey, state.Record != null))
            return true;

        if (state.RecordListing != null)
            _selection.SetListing(state.RecordListing);

        return false;
    }
}
