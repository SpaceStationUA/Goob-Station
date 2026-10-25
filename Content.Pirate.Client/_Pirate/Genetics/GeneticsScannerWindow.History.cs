// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Genetics.Console;

namespace Content.Pirate.Client.Genetics.UI;

public sealed partial class GeneticsScannerWindow
{
    public event Action<int?>? OnSelectScan;

    private HandheldGeneticsHistoryState? _history;
    private bool _hasStoredSequences;

    private void InitializeHistory()
    {
        Sequencer.HideHandheldScanButton();
        HistoryTarget.OnItemSelected += args => OnSelectScan?.Invoke(args.Id);
    }

    private void UpdateHistoryState(GeneticsConsoleState state)
    {
        _history = state.HandheldHistory;
        _hasStoredSequences = state.Sequences.Count != 0;
        HistoryContainer.Visible = _history != null;
        HistoryTarget.Clear();
        if (_history == null)
            return;

        for (var i = 0; i < _history.DnaIds.Count; i++)
            HistoryTarget.AddItem(Loc.GetString("genetics-scanner-dna-title", ("dna", _history.DnaIds[i])), i);
        if (_history.SelectedScan is {} selected)
            HistoryTarget.SelectId(selected);
        UpdateHistoryView();
    }

    private void UpdateHistoryView()
    {
        HistoryTarget.Disabled = _busy || HistoryTarget.ItemCount == 0;
        Sequencer.ShowHandheldScan(_history?.SelectedScan != null, _hasStoredSequences, _busy);
    }
}

public sealed partial class GeneticSequencer
{
    public void HideHandheldScanButton()
    {
        Scanner.HideHandheldScanButton();
    }

    public void ShowHandheldScan(bool hasRecord, bool hasSequences, bool busy)
    {
        Scanner.SetError(hasRecord
            ? hasSequences ? null : "genetics-console-sequencer-no-sequences"
            : busy ? "genetics-scanner-scanning" : "genetics-scanner-use-on-subject");
    }
}

public sealed partial class GeneticScanner
{
    public void HideHandheldScanButton()
    {
        ScanButton.Visible = false;
    }
}
