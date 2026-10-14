// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.StationRecords;

public sealed class RecordTabs
{
    private readonly Button _profileButton;
    private readonly Button _historyButton;
    private readonly Control _profileView;
    private readonly Control _historyView;

    private OptionButton? _filterType;
    private LineEdit? _recordSearch;
    private LineEdit? _historySearch;
    private RecordHistoryPanel? _history;
    private bool _hasRecord = true;
    private bool _searchingHistory;

    public bool ShowingHistory { get; private set; }

    public RecordTabs(Button profileButton, Button historyButton, Control profileView, Control historyView)
    {
        _profileButton = profileButton;
        _historyButton = historyButton;
        _profileView = profileView;
        _historyView = historyView;

        var group = new ButtonGroup();
        profileButton.Group = group;
        historyButton.Group = group;
        profileButton.OnPressed += _ => Show(history: false);
        historyButton.OnPressed += _ => Show(history: true);
        Show(history: false);
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
        Show(ShowingHistory);
    }

    private void Show(bool history)
    {
        ShowingHistory = history;
        _profileButton.Pressed = !history;
        _historyButton.Pressed = history;
        _profileView.Visible = !history;
        _historyView.Visible = history;
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
