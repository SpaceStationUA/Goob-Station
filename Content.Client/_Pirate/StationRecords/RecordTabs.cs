// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.StationRecords;

public sealed class RecordTabs
{
    private const int HistoryTab = 1;

    private readonly ButtonGroup _group = new();
    private readonly List<(Button Button, Control View)> _tabs = new();
    private int _shown;

    private OptionButton? _filterType;
    private LineEdit? _recordSearch;
    private LineEdit? _historySearch;
    private RecordHistoryPanel? _history;
    private bool _hasRecord = true;
    private bool _searchingHistory;

    public bool ShowingHistory => _shown == HistoryTab;

    public RecordTabs(Button profileButton, Button historyButton, Control profileView, Control historyView)
    {
        AddTab(profileButton, profileView);
        AddTab(historyButton, historyView);
        Show(0);
    }

    public void AddTab(Button button, Control view)
    {
        var index = _tabs.Count;
        _tabs.Add((button, view));
        button.Group = _group;
        button.OnPressed += _ => Show(index);
        button.Pressed = index == _shown;
        view.Visible = index == _shown;
    }

    public void BindSearch(OptionButton filterType, LineEdit recordSearch, RecordHistoryPanel history)
    {
        _filterType = filterType;
        _recordSearch = recordSearch;
        _history = history;

        _historySearch = new LineEdit
        {
            PlaceHolder = Loc.GetString("records-log-search-placeholder"),
            HorizontalExpand = recordSearch.HorizontalExpand,
            Margin = recordSearch.Margin,
            Visible = false,
        };
        _historySearch.OnTextEntered += args => _history.SetQuery(args.Text);

        var parent = recordSearch.Parent!;
        parent.AddChild(_historySearch);
        _historySearch.SetPositionInParent(recordSearch.GetPositionInParent() + 1);

        UpdateSearch();
    }

    public void SetHasRecord(bool hasRecord)
    {
        _hasRecord = hasRecord;
        UpdateSearch();
    }

    public void Refresh()
    {
        Show(_shown);
    }

    private void Show(int index)
    {
        _shown = index;
        for (var i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Button.Pressed = i == index;
            _tabs[i].View.Visible = i == index;
        }

        UpdateSearch();
    }

    private void UpdateSearch()
    {
        if (_historySearch == null || _recordSearch == null || _filterType == null)
            return;

        var searching = ShowingHistory && _hasRecord;
        if (searching == _searchingHistory)
            return;

        _searchingHistory = searching;
        // Keep the record query while history search uses its own field.
        _recordSearch.Visible = !searching;
        _historySearch.Visible = searching;
        _filterType.Disabled = searching;

        if (searching)
        {
            _historySearch.GrabKeyboardFocus();
            return;
        }

        _historySearch.Text = string.Empty;
        _history?.SetQuery(string.Empty);
    }
}
