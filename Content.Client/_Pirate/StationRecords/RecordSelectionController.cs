// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Pirate.StationRecords;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.StationRecords;

public sealed class RecordSelectionController<TState> where TState : class
{
    private static readonly TimeSpan PendingSelectionTimeout = TimeSpan.FromSeconds(2);

    private readonly ItemList _list;
    private readonly IGameTiming _timing;
    private readonly IRobustRandom _random;

    private readonly List<(uint Key, uint Request)> _pendingSelections = new();
    private readonly Dictionary<uint, TState> _stateCache = new();
    private TimeSpan _lastSelectionRequest;
    private uint? _serverSelectedKey;
    private bool _syncingHighlight;

    public event Action<uint>? SelectionRequested;

    public event Action<TState, TState>? ShowCached;

    public TState? LastServerState { get; private set; }

    public bool ApplyingLocalState { get; private set; }

    public uint? RequestedKey => _pendingSelections.Count > 0 ? _pendingSelections[^1].Key : _serverSelectedKey;

    // The server echoes this ID to correlate selection replies.
    public uint LastRequest { get; private set; }

    public RecordSelectionController(ItemList list)
    {
        _list = list;
        _timing = IoCManager.Resolve<IGameTiming>();
        _random = IoCManager.Resolve<IRobustRandom>();
        _list.OnItemSelected += OnItemSelected;
        _list.OnItemDeselected += OnItemDeselected;
    }

    public bool AcceptServerState(TState state, uint? selectedKey, uint handledRequest, bool hasRecord)
    {
        if (ApplyingLocalState)
            return true;

        LastServerState = state;
        if (selectedKey is { } selected && hasRecord)
            _stateCache[selected] = state;

        var serverChanged = selectedKey != _serverSelectedKey;
        _serverSelectedKey = selectedKey;

        if (_pendingSelections.Count == 0)
            return true;

        // Match request IDs because one reply may confirm several queued clicks.
        var confirmed = _pendingSelections.FindIndex(p => p.Request == handledRequest);
        if (confirmed >= 0)
        {
            _pendingSelections.RemoveRange(0, confirmed + 1);
        }
        else if (serverChanged || _timing.RealTime - _lastSelectionRequest > PendingSelectionTimeout)
            _pendingSelections.Clear(); // rejected (record gone), or someone else at this console picked one

        return _pendingSelections.Count == 0;
    }

    public void SetListing(IReadOnlyDictionary<uint, string>? listing)
    {
        if (listing == null)
        {
            _list.Clear();
            return;
        }

        var entries = listing.Select(i => new ItemList.Item(_list)
        {
            Text = i.Value,
            Metadata = i.Key,
        }).ToList();
        entries.Sort(CompareItems);
        _list.SetItems(entries, CompareItems);
        SyncHighlight();
    }

    public void SyncHighlight()
    {
        var key = RequestedKey;
        _syncingHighlight = true;
        try
        {
            foreach (var item in _list)
            {
                item.Selected = key != null && item.Metadata is uint itemKey && itemKey == key;
            }
        }
        finally
        {
            _syncingHighlight = false;
        }
    }

    public void ClearCache()
    {
        _stateCache.Clear();
    }

    private void OnItemSelected(ItemList.ItemListSelectedEventArgs args)
    {
        if (_syncingHighlight || _list[args.ItemIndex].Metadata is not uint key)
            return;

        RequestSelection(key);
    }

    private void OnItemDeselected(ItemList.ItemListDeselectedEventArgs args)
    {
        if (_syncingHighlight)
            return;

        // ItemList deselects the old row before selecting the new one and toggles the current row off on a re-click.
        if (_list.GetSelected().Any())
            return;

        if (_list[args.ItemIndex].Metadata is uint key)
            RequestSelection(key);

        SyncHighlight();
    }

    private void RequestSelection(uint key)
    {
        if (key == RequestedKey)
            return;

        LastRequest = (uint) _random.Next(1, int.MaxValue);
        _pendingSelections.Add((key, LastRequest));
        _lastSelectionRequest = _timing.RealTime;
        SelectionRequested?.Invoke(key);
        RecordConsoleSounds.Play(_list, RecordConsoleSound.SwitchRecord);

        if (LastServerState is not { } last || !_stateCache.TryGetValue(key, out var cached))
            return;

        ApplyingLocalState = true;
        try
        {
            ShowCached?.Invoke(cached, last);
        }
        finally
        {
            ApplyingLocalState = false;
        }
    }

    private static int CompareItems(ItemList.Item a, ItemList.Item b)
    {
        var text = string.Compare(a.Text, b.Text, StringComparison.Ordinal);
        if (text != 0)
            return text;

        return ((uint?) a.Metadata ?? 0).CompareTo((uint?) b.Metadata ?? 0);
    }
}
