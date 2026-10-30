// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Pirate.CriminalRecords;
using Content.Shared.CriminalRecords;

namespace Content.Client.CriminalRecords;

public sealed partial class CriminalRecordsConsoleWindow
{
    public event Action<string, string, int>? OnAddCitation;
    public event Action<uint>? OnPayCitation;
    public event Action<uint>? OnVoidCitation;

    private void InitCitations()
    {
        _tabs.AddTab(CitationsTabButton, CitationsView);
        CitationsView.OnAddCitation += (reason, details, fine) => OnAddCitation?.Invoke(reason, details, fine);
        CitationsView.OnPay += id => OnPayCitation?.Invoke(id);
        CitationsView.OnVoid += id => OnVoidCitation?.Invoke(id);
    }

    private void UpdateCitations(uint key, string name, CriminalRecord record)
    {
        CitationsView.UpdateCitations(key, name, record.Citations, _access);

        var active = RecordCitationsPanel.ActiveCount(record.Citations);
        CitationsTabButton.Text = active > 0
            ? Loc.GetString("criminal-records-citation-tab-count", ("count", active))
            : Loc.GetString("criminal-records-citation-tab");
    }

    private void ClearCitations()
    {
        CitationsView.CloseDialog();
        CitationsTabButton.Text = Loc.GetString("criminal-records-citation-tab");
    }
}
