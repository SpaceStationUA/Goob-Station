// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Pirate.Shared.Genetics.Console;
using Robust.Client.UserInterface.Controls;

namespace Content.Pirate.Client.Genetics.UI;

public sealed partial class GeneticsScannerWindow
{
    public event Action<int?>? OnSelectScan;

    private enum TargetSearch
    {
        All,
        Name,
        Sequence
    }

    private HandheldGeneticsHistoryState? _history;
    private bool _hasStoredSequences;
    private bool _syncingTargets;

    private void InitializeHistory()
    {
        Sequencer.HideHandheldScanButton();

        SearchMode.AddItem(Loc.GetString("genetics-scanner-search-all"), (int) TargetSearch.All);
        SearchMode.AddItem(Loc.GetString("genetics-scanner-search-name"), (int) TargetSearch.Name);
        SearchMode.AddItem(Loc.GetString("genetics-scanner-search-sequence"), (int) TargetSearch.Sequence);
        SearchMode.OnItemSelected += args =>
        {
            SearchMode.SelectId(args.Id);
            UpdateTargetList();
        };
        SearchText.OnTextChanged += _ => UpdateTargetList();

        HistoryTarget.OnItemSelected += args =>
        {
            if (_syncingTargets || _busy || HistoryTarget[args.ItemIndex].Metadata is not int index)
                return;

            if (index != _history?.SelectedScan)
                OnSelectScan?.Invoke(index);
        };
        // The shown target cannot be deselected.
        HistoryTarget.OnItemDeselected += _ =>
        {
            if (!_syncingTargets && !HistoryTarget.GetSelected().Any())
                SyncTargetSelection();
        };
    }

    private void UpdateHistoryState(GeneticsConsoleState state)
    {
        _history = state.HandheldHistory;
        _hasStoredSequences = state.Sequences.Count != 0;
        HistoryContainer.Visible = _history != null;
        UpdateTargetList();
        UpdateHistoryView();
    }

    private void UpdateTargetList()
    {
        var items = new List<ItemList.Item>();
        if (_history != null)
        {
            var mode = (TargetSearch) SearchMode.SelectedId;
            var search = SearchText.Text.Trim();
            for (var i = 0; i < _history.Targets.Count; i++)
            {
                var target = _history.Targets[i];
                if (!Matches(target, mode, search))
                    continue;

                items.Add(new ItemList.Item(HistoryTarget)
                {
                    Text = target.Name,
                    Metadata = i
                });
            }
        }

        items.Sort((a, b) => string.Compare(a.Text, b.Text, StringComparison.CurrentCultureIgnoreCase));
        _syncingTargets = true;
        try
        {
            HistoryTarget.SetItems(items, (a, b) => string.Compare(a.Text, b.Text, StringComparison.CurrentCultureIgnoreCase));
        }
        finally
        {
            _syncingTargets = false;
        }
        NoTargets.Visible = _history != null && _history.Targets.Count == 0;
        SyncTargetSelection();
    }

    private static bool Matches(HandheldGeneticsTarget target, TargetSearch mode, string search)
    {
        if (search.Length == 0)
            return true;

        var name = mode != TargetSearch.Sequence &&
            target.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase);
        var sequence = mode != TargetSearch.Name &&
            int.TryParse(search, out var number) && target.Numbers.Contains(number);
        return name || sequence;
    }

    private void SyncTargetSelection()
    {
        _syncingTargets = true;
        try
        {
            foreach (var item in HistoryTarget)
            {
                item.Selected = item.Metadata is int index && index == _history?.SelectedScan;
            }
        }
        finally
        {
            _syncingTargets = false;
        }
    }

    private void UpdateHistoryView()
    {
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
