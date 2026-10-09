// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.CustomMarkings;

namespace Content.Client._Pirate.CustomMarkings;

// The local player's library of saved markings, kept by the server.
public sealed partial class CustomMarkingSystem
{
    private List<CustomMarkingEntry>? _library;
    private int _lastRequest;

    /// <summary>The local player's saved markings, or null until the server has sent them.</summary>
    public IReadOnlyList<CustomMarkingEntry>? Library => _library;

    public event Action? LibraryUpdated;

    /// <summary>Raised with the server's answer to a <see cref="Save"/>.</summary>
    public event Action<CustomMarkingSaveResultEvent>? SaveAnswered;

    private void InitializeLibrary()
    {
        SubscribeNetworkEvent<CustomMarkingLibraryEvent>(OnLibrary);
        SubscribeNetworkEvent<CustomMarkingSaveResultEvent>(OnSaveResult);
    }

    public void RequestLibrary()
    {
        RaiseNetworkEvent(new CustomMarkingLibraryRequestEvent());
    }

    /// <summary>
    /// Saves a marking to the library: a new one for id 0, otherwise a change to that entry. Null art keeps the
    /// entry's own. Returns the request number its <see cref="SaveAnswered"/> will carry.
    /// </summary>
    public int Save(int id, string name, CustomMarkingPlacement placement, CustomMarkingArt? art)
    {
        var request = ++_lastRequest;
        RaiseNetworkEvent(new CustomMarkingSaveEvent(
            request,
            id,
            name,
            placement,
            art?.Pixels,
            art?.GetFrameTimes(),
            art != null && art.HasErase() ? art.Erase : null));
        return request;
    }

    public void Delete(int id)
    {
        RaiseNetworkEvent(new CustomMarkingDeleteEvent(id));
    }

    private void OnLibrary(CustomMarkingLibraryEvent ev)
    {
        _library = ev.Entries;
        LibraryUpdated?.Invoke();
    }

    private void OnSaveResult(CustomMarkingSaveResultEvent ev)
    {
        SaveAnswered?.Invoke(ev);
    }
}
